using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

public class MonthlySummaryService(AppDbContext db, IEmailSender email, IConfiguration config)
{
    public const string EmailType = "MonthlySummary";

    public async Task<MonthlySummaryDto> BuildAsync(int year, int month) => (await LoadAsync(year, month)).Summary;

    public async Task<byte[]> BuildExcelAsync(int year, int month)
    {
        var (summary, dailyOutput) = await LoadAsync(year, month);
        return MonthlySummaryExcel.Build(summary, dailyOutput);
    }

    private async Task<(MonthlySummaryDto Summary, Dictionary<int, Dictionary<int, int>> DailyOutput)> LoadAsync(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var attendance = await db.Attendance.AsNoTracking().Where(a => a.Date >= from && a.Date <= to).ToListAsync();
        var statuses = await db.DailyStatuses.AsNoTracking().Where(s => s.Date >= from && s.Date <= to).ToListAsync();
        var employees = await db.Employees.AsNoTracking().ToListAsync();

        var locked = await db.MonthLocks.AnyAsync(l => l.Year == year && l.Month == month);
        var sentAt = await db.EmailLogs
            .Where(l => l.Type == EmailType && l.ForDate == from && l.Success)
            .MaxAsync(l => (DateTime?)l.SentAt);

        var summary = new MonthlySummaryDto(
            year,
            month,
            locked,
            sentAt is null ? null : DateTime.SpecifyKind(sentAt.Value, DateTimeKind.Utc),
            MonthlySummaryRules.TeamAverage(statuses, attendance),
            MonthlySummaryRules.BuildRows(employees, attendance, statuses));
        return (summary, MonthlySummaryRules.DailyOutput(attendance, statuses));
    }

    /// <summary>שולח את הסיכום עם קובץ אקסל למנהלות הצוות.</summary>
    public async Task<ServiceError?> SendAsync(int year, int month, CancellationToken ct = default)
    {
        var to = await db.Users
            .Where(u => u.Role == UserRole.TeamLead && u.Email != "")
            .Select(u => u.Email)
            .ToListAsync(ct);
        if (to.Count == 0) return new ServiceError("אין מנהלת צוות עם כתובת מייל");

        var (summary, dailyOutput) = await LoadAsync(year, month);
        var baseUrl = config["App:BaseUrl"]?.TrimEnd('/');
        var link = string.IsNullOrEmpty(baseUrl) ? null : $"{baseUrl}/bonuses?month={year}-{month:00}";

        var error = await email.SendAsync(new EmailMessage(
            EmailType,
            to,
            MonthlySummaryRules.Subject(year, month),
            MonthlySummaryRules.Html(summary, link),
            new DateOnly(year, month, 1),
            [new EmailAttachment(MonthlySummaryExcel.FileName(year, month), MonthlySummaryExcel.Build(summary, dailyOutput), MonthlySummaryExcel.ContentType)]),
            ct);

        return error is null
            ? null
            : new ServiceError("שליחת המייל נכשלה. אפשר לנסות לשלוח שוב.", StatusCodes.Status502BadGateway);
    }

    /// <summary>
    /// נקרא כל דקה משירות הרקע: מ-1 לחודש ב-08:00 שולח את הסיכום של החודש הקודם, פעם אחת.
    /// גם ניסיון שנכשל נחשב, כדי לא להציף; אפשר לשלוח שוב ידנית ממסך הסיכום.
    /// </summary>
    public async Task SendIfDueAsync(DateTime localNow, CancellationToken ct)
    {
        if (!MonthlySummaryRules.IsSummaryDue(localNow)) return;

        var (year, month) = MonthlySummaryRules.PreviousMonth(DateOnly.FromDateTime(localNow));
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        if (await db.EmailLogs.AnyAsync(l => l.Type == EmailType && l.ForDate == from, ct)) return;
        // חודש בלי שום נוכחות (למשל לפני שהמערכת התחילה לפעול) לא נשלח.
        if (!await db.Attendance.AnyAsync(a => a.Date >= from && a.Date <= to, ct)) return;

        await SendAsync(year, month, ct);
    }
}
