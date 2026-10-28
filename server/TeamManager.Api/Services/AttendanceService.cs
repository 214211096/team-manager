using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>שגיאה עסקית שמוחזרת למשתמשת כ-ProblemDetails.</summary>
public record ServiceError(string Message, int StatusCode = StatusCodes.Status400BadRequest);

public class AttendanceService(AppDbContext db, SettingsService settings, IEmailSender email, ScheduleService schedule)
{
    public const string ReportEmailType = "AttendanceReport";
    public const string ReminderEmailType = "AttendanceReminder";

    public Task<bool> IsLockedAsync(DateOnly date) =>
        db.MonthLocks.AnyAsync(l => l.Year == date.Year && l.Month == date.Month);

    /// <summary>כל העובדות הפעילות, וגם לא פעילות שיש להן רשומה באותו יום.</summary>
    public async Task<AttendanceDayDto> GetDayAsync(DateOnly date)
    {
        var records = await db.Attendance.AsNoTracking()
            .Where(a => a.Date == date)
            .ToDictionaryAsync(a => a.EmployeeId);
        var recordedIds = records.Keys.ToList();

        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.IsActive || recordedIds.Contains(e.Id))
            .OrderBy(e => e.Name)
            .ToListAsync();

        var planned = await schedule.ForDayAsync(date);

        var rows = employees
            .Select(e =>
            {
                var r = records.GetValueOrDefault(e.Id);
                var p = planned.GetValueOrDefault(e.Id);
                return new AttendanceRowDto(e.Id, e.Name, e.IsActive, r?.Status, r?.OtherReason,
                    p is null ? null : ScheduleRules.ToPlanned(p));
            })
            .ToList();

        return new AttendanceDayDto(date, await IsLockedAsync(date), await ReportSentAtAsync(date), rows);
    }

    public async Task<ServiceError?> SaveAsync(DateOnly date, IReadOnlyList<AttendanceEntryRequest> entries)
    {
        var ids = entries.Select(e => e.EmployeeId).ToList();
        var knownCount = await db.Employees.CountAsync(e => ids.Contains(e.Id));
        if (knownCount != ids.Count) return new ServiceError("אחת העובדות לא נמצאה");

        var existing = await db.Attendance
            .Where(a => a.Date == date && ids.Contains(a.EmployeeId))
            .ToDictionaryAsync(a => a.EmployeeId);

        foreach (var entry in entries)
        {
            if (!existing.TryGetValue(entry.EmployeeId, out var row))
            {
                row = new Attendance { EmployeeId = entry.EmployeeId, Date = date };
                db.Attendance.Add(row);
            }
            // הסיבה נשמרת רק ב"אחר", כדי שלא יישארו פרטים מיותרים אחרי שינוי סטטוס.
            var reason = entry.Status == AttendanceStatus.Other ? entry.OtherReason!.Trim() : null;

            // UpdatedAt משתנה רק בשינוי אמיתי: לפיו מייל העדכון יודע מה השתנה מאז השליחה הקודמת.
            if (row.Id != 0 && row.Status == entry.Status && row.OtherReason == reason) continue;
            row.Status = entry.Status;
            row.OtherReason = reason;
            row.UpdatedAt = DateTime.UtcNow;
        }

        // עובדת שסומנה נעדרת לא נספרת בסטטוס היומי, אז מוחקים ביצוע שהוזן לה קודם באותו יום.
        var absentIds = entries.Where(e => e.Status != AttendanceStatus.Present).Select(e => e.EmployeeId).ToList();
        if (absentIds.Count > 0)
            db.DailyStatuses.RemoveRange(
                await db.DailyStatuses.Where(d => d.Date == date && absentIds.Contains(d.EmployeeId)).ToListAsync());

        await db.SaveChangesAsync();
        return null;
    }

    public async Task<ServiceError?> SendReportAsync(DateOnly date)
    {
        var managerEmail = (await settings.GetAsync()).ManagerEmail;
        if (string.IsNullOrWhiteSpace(managerEmail))
            return new ServiceError("לא הוגדר מייל מנהלת. יש להגדיר אותו במסך ההגדרות.");

        var day = await GetDayAsync(date);
        var missing = day.Rows.Where(r => r.IsActive && r.Status is null).Select(r => r.EmployeeName).ToList();
        if (missing.Count > 0)
            return new ServiceError($"חסר דיווח עבור: {string.Join(", ", missing)}");

        var rows = day.Rows.Where(r => r.Status is not null).ToList();
        if (rows.Count == 0) return new ServiceError("אין נוכחות לשליחה");

        List<AttendanceRowDto>? changed = null;
        if (day.ReportSentAt is { } lastSent)
        {
            var changedIds = await db.Attendance
                .Where(a => a.Date == date && a.UpdatedAt > lastSent)
                .Select(a => a.EmployeeId)
                .ToListAsync();
            changed = rows.Where(r => changedIds.Contains(r.EmployeeId)).ToList();
        }

        var error = await email.SendAsync(new EmailMessage(
            ReportEmailType,
            [managerEmail],
            AttendanceRules.ReportSubject(date, isUpdate: changed is not null),
            AttendanceRules.ReportHtml(date, rows, changed),
            date));

        return error is null
            ? null
            : new ServiceError("שליחת המייל נכשלה. הנוכחות שמורה, אפשר לנסות לשלוח שוב.", StatusCodes.Status502BadGateway);
    }

    /// <summary>
    /// נקרא כל דקה משירות הרקע. שולח תזכורת אחת ביום למנהלות הצוות אם הדיווח של היום לא נשלח.
    /// גם ניסיון שנכשל נחשב, כדי לא להציף; הכשל נשאר ב-EmailLog.
    /// </summary>
    public async Task SendReminderIfDueAsync(DateTime localNow, CancellationToken ct)
    {
        var s = await settings.GetAsync();
        if (!TimeOnly.TryParseExact(s.ReminderTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) return;
        if (!AttendanceRules.IsReminderDue(localNow, at, s.NoReminderDates)) return;

        var today = DateOnly.FromDateTime(localNow);
        var handled = await db.EmailLogs.AnyAsync(l => l.ForDate == today &&
            ((l.Type == ReportEmailType && l.Success) || l.Type == ReminderEmailType), ct);
        if (handled) return;

        if (!await db.Employees.AnyAsync(e => e.IsActive, ct)) return;

        var to = await db.Users
            .Where(u => u.Role == UserRole.TeamLead && u.Email != "")
            .Select(u => u.Email)
            .ToListAsync(ct);
        if (to.Count == 0) return;

        await email.SendAsync(new EmailMessage(
            ReminderEmailType, to, AttendanceRules.ReminderSubject, AttendanceRules.ReminderHtml(today), today), ct);
    }

    private async Task<DateTime?> ReportSentAtAsync(DateOnly date)
    {
        var sentAt = await db.EmailLogs
            .Where(l => l.Type == ReportEmailType && l.ForDate == date && l.Success)
            .MaxAsync(l => (DateTime?)l.SentAt);
        // נשמר ב-UTC; מסמנים כך שה-JSON יכלול Z והלקוח יציג בשעה מקומית.
        return sentAt is null ? null : DateTime.SpecifyKind(sentAt.Value, DateTimeKind.Utc);
    }
}
