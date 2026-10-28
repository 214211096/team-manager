using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>ביצוע יומי מול יעד, רק לעובדות שסומנו "נוכחת" באותו יום.</summary>
public class DailyStatusService(AppDbContext db, SettingsService settings, ScheduleService schedule)
{
    public async Task<DailyStatusDayDto> GetDayAsync(DateOnly date)
    {
        var s = await settings.GetAsync();

        var attendance = await db.Attendance.AsNoTracking()
            .Where(a => a.Date == date)
            .Select(a => new { a.EmployeeId, a.Status })
            .ToListAsync();
        var presentIds = attendance.Where(a => a.Status == AttendanceStatus.Present).Select(a => a.EmployeeId).ToList();

        var employees = await db.Employees.AsNoTracking()
            .Where(e => presentIds.Contains(e.Id))
            .OrderBy(e => e.Name)
            .ToListAsync();
        var statuses = await db.DailyStatuses.AsNoTracking()
            .Where(d => d.Date == date && presentIds.Contains(d.EmployeeId))
            .ToDictionaryAsync(d => d.EmployeeId);
        var planned = await schedule.ForDayAsync(date);

        var rows = employees
            .Select(e =>
            {
                if (statuses.GetValueOrDefault(e.Id) is { } d)
                    return new DailyStatusRowDto(e.Id, e.Name, d.TargetSnapshot, d.Completed, d.Percent, d.Rating, d.WorkPercent);
                var workPercent = ScheduleRules.WorkPercentFor(planned.GetValueOrDefault(e.Id));
                return new DailyStatusRowDto(
                    e.Id, e.Name, ScheduleRules.EffectiveTarget(e.DailyTarget, workPercent), null, null, null, workPercent);
            })
            .ToList();

        var locked = await db.MonthLocks.AnyAsync(l => l.Year == date.Year && l.Month == date.Month);
        return new DailyStatusDayDto(date, locked, s.WeakThreshold, s.ExcellentThreshold, attendance.Count, rows);
    }

    public async Task<ServiceError?> SaveAsync(DateOnly date, IReadOnlyList<DailyStatusEntryRequest> entries)
    {
        var ids = entries.Select(e => e.EmployeeId).ToList();
        if (ids.Distinct().Count() != ids.Count) return new ServiceError("עובדת מופיעה יותר מפעם אחת");

        var presentCount = await db.Attendance.CountAsync(a =>
            a.Date == date && a.Status == AttendanceStatus.Present && ids.Contains(a.EmployeeId));
        if (presentCount != ids.Count)
            return new ServiceError("אפשר להזין ביצוע רק לעובדת שסומנה נוכחת באותו יום");

        var s = await settings.GetAsync();
        var targets = await db.Employees.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.DailyTarget);
        var existing = await db.DailyStatuses
            .Where(d => d.Date == date && ids.Contains(d.EmployeeId))
            .ToDictionaryAsync(d => d.EmployeeId);
        var planned = await schedule.ForDayAsync(date);

        foreach (var entry in entries)
        {
            var row = existing.GetValueOrDefault(entry.EmployeeId);
            if (entry.Completed is not { } completed)
            {
                if (row is not null) db.DailyStatuses.Remove(row);
                continue;
            }

            if (row is null)
            {
                // היעד המלא של היום נקבע בהזנה הראשונה ולא משתנה אחר כך, גם אם היעד של העובדת משתנה.
                // ביום חלקי (מלוח השנה) היעד בפועל מוקטן לפי אחוז העבודה.
                var workPercent = ScheduleRules.WorkPercentFor(planned.GetValueOrDefault(entry.EmployeeId));
                row = new DailyStatus
                {
                    EmployeeId = entry.EmployeeId,
                    Date = date,
                    BaseTarget = targets[entry.EmployeeId],
                    WorkPercent = workPercent,
                    TargetSnapshot = ScheduleRules.EffectiveTarget(targets[entry.EmployeeId], workPercent),
                };
                db.DailyStatuses.Add(row);
            }

            var (percent, rating) = StatusCalculator.Calculate(completed, row.TargetSnapshot, s.WeakThreshold, s.ExcellentThreshold);
            row.Completed = completed;
            row.Percent = percent;
            row.Rating = rating;
        }

        await db.SaveChangesAsync();
        return null;
    }
}
