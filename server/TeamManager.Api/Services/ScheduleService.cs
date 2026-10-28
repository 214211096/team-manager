using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>לוח שנה: היעדרויות וימים חלקיים שמוזנים מראש.</summary>
public class ScheduleService(AppDbContext db, SettingsService settings)
{
    public async Task<IReadOnlyList<ScheduleEntryDto>> ListAsync(DateOnly from, DateOnly to) =>
        await db.ScheduleEntries.AsNoTracking()
            .Where(s => s.StartDate <= to && s.EndDate >= from)
            .OrderBy(s => s.StartDate).ThenBy(s => s.Employee.Name)
            .Select(s => new ScheduleEntryDto(
                s.Id, s.EmployeeId, s.Employee.Name, s.StartDate, s.EndDate, s.Kind, s.AbsenceStatus, s.Reason, s.WorkPercent))
            .ToListAsync();

    /// <summary>הרישום שחל על כל עובדת בתאריך (לכל היותר אחד, כי אין חפיפות).</summary>
    public async Task<Dictionary<int, ScheduleEntry>> ForDayAsync(DateOnly date) =>
        (await db.ScheduleEntries.AsNoTracking()
            .Where(s => s.StartDate <= date && s.EndDate >= date)
            .ToListAsync())
        .GroupBy(s => s.EmployeeId)
        .ToDictionary(g => g.Key, g => g.First());

    public async Task<(ScheduleEntryDto? Entry, ServiceError? Error)> SaveAsync(int? id, ScheduleEntryRequest req)
    {
        ScheduleEntry? entry = null;
        if (id is not null)
        {
            entry = await db.ScheduleEntries.FindAsync(id);
            if (entry is null) return (null, new ServiceError("הרישום לא נמצא", StatusCodes.Status404NotFound));
            if (await TouchesLockedMonthAsync(entry.StartDate, entry.EndDate)) return (null, LockedError);
        }

        if (await TouchesLockedMonthAsync(req.StartDate, req.EndDate)) return (null, LockedError);
        if (!await db.Employees.AnyAsync(e => e.Id == req.EmployeeId)) return (null, new ServiceError("העובדת לא נמצאה"));

        var overlaps = await db.ScheduleEntries.AnyAsync(s =>
            s.EmployeeId == req.EmployeeId && s.Id != (id ?? 0) && s.StartDate <= req.EndDate && s.EndDate >= req.StartDate);
        if (overlaps) return (null, new ServiceError("לעובדת כבר יש רישום בחלק מהתאריכים האלה"));

        // הטווח הקודם, כדי לחשב מחדש גם ימים שיצאו מהרישום.
        var previous = entry is null ? null : new { entry.EmployeeId, entry.StartDate, entry.EndDate };

        if (entry is null)
        {
            entry = new ScheduleEntry();
            db.ScheduleEntries.Add(entry);
        }
        entry.EmployeeId = req.EmployeeId;
        entry.StartDate = req.StartDate;
        entry.EndDate = req.EndDate;
        entry.Kind = req.Kind;
        // שומרים רק את השדות שרלוונטיים לסוג, כדי שלא יישארו נתונים מיותרים.
        entry.AbsenceStatus = req.Kind == ScheduleKind.Absence ? req.AbsenceStatus : null;
        entry.Reason = req is { Kind: ScheduleKind.Absence, AbsenceStatus: AttendanceStatus.Other } ? req.Reason!.Trim() : null;
        entry.WorkPercent = req.Kind == ScheduleKind.PartialDay ? req.WorkPercent : null;
        await db.SaveChangesAsync();

        if (previous is not null) await RecalculateAsync(previous.EmployeeId, previous.StartDate, previous.EndDate);
        await RecalculateAsync(entry.EmployeeId, entry.StartDate, entry.EndDate);

        var name = await db.Employees.Where(e => e.Id == entry.EmployeeId).Select(e => e.Name).SingleAsync();
        return (new ScheduleEntryDto(entry.Id, entry.EmployeeId, name, entry.StartDate, entry.EndDate,
            entry.Kind, entry.AbsenceStatus, entry.Reason, entry.WorkPercent), null);
    }

    public async Task<ServiceError?> DeleteAsync(int id)
    {
        var entry = await db.ScheduleEntries.FindAsync(id);
        if (entry is null) return new ServiceError("הרישום לא נמצא", StatusCodes.Status404NotFound);
        if (await TouchesLockedMonthAsync(entry.StartDate, entry.EndDate)) return LockedError;

        db.ScheduleEntries.Remove(entry);
        await db.SaveChangesAsync();
        await RecalculateAsync(entry.EmployeeId, entry.StartDate, entry.EndDate);
        return null;
    }

    private static readonly ServiceError LockedError =
        new("הרישום כולל חודש שנעול אחרי שליחה לשכר", StatusCodes.Status409Conflict);

    private async Task<bool> TouchesLockedMonthAsync(DateOnly from, DateOnly to)
    {
        var locks = await db.MonthLocks.Select(l => new { l.Year, l.Month }).ToListAsync();
        return locks.Any(l =>
        {
            var monthStart = new DateOnly(l.Year, l.Month, 1);
            return monthStart <= to && monthStart.AddMonths(1) > from;
        });
    }

    /// <summary>
    /// מעדכן יעד, אחוז וסטטוס של ביצוע שכבר הוזן בטווח, לפי מה שמתוכנן עכשיו.
    /// היעד המלא (BaseTarget) לא משתנה, כך ששינוי יעד של העובדת עדיין לא משפיע על העבר.
    /// </summary>
    private async Task RecalculateAsync(int employeeId, DateOnly from, DateOnly to)
    {
        var rows = await db.DailyStatuses
            .Where(d => d.EmployeeId == employeeId && d.Date >= from && d.Date <= to)
            .ToListAsync();
        if (rows.Count == 0) return;

        var entries = await db.ScheduleEntries.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId && s.StartDate <= to && s.EndDate >= from)
            .ToListAsync();
        var s = await settings.GetAsync();

        foreach (var row in rows)
        {
            var planned = entries.FirstOrDefault(e => e.StartDate <= row.Date && e.EndDate >= row.Date);
            row.WorkPercent = ScheduleRules.WorkPercentFor(planned);
            row.TargetSnapshot = ScheduleRules.EffectiveTarget(row.BaseTarget, row.WorkPercent);
            (row.Percent, row.Rating) = StatusCalculator.Calculate(row.Completed, row.TargetSnapshot, s.WeakThreshold, s.ExcellentThreshold);
        }
        await db.SaveChangesAsync();
    }
}
