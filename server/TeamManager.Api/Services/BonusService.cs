using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>בונוסים חודשיים ושליחה לשכר עם נעילת החודש.</summary>
public class BonusService(
    AppDbContext db,
    SettingsService settings,
    MonthlySummaryService summary,
    IEmailSender email,
    TimeProvider clock)
{
    public const string EmailType = "BonusPayroll";

    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public async Task<BonusMonthDto> GetAsync(int year, int month)
    {
        var s = await settings.GetAsync();
        var sum = await summary.BuildAsync(year, month);
        var bonuses = await db.MonthlyBonuses.AsNoTracking()
            .Where(b => b.Year == year && b.Month == month)
            .ToDictionaryAsync(b => b.EmployeeId);
        var monthLock = await db.MonthLocks.AsNoTracking().SingleOrDefaultAsync(l => l.Year == year && l.Month == month);

        // עובדת לא פעילה שכבר יש לה בונוס בחודש מופיעה, גם אם אין לה נתוני נוכחות.
        var missingIds = bonuses.Keys.Except(sum.Rows.Select(r => r.EmployeeId)).ToList();
        var extra = await db.Employees.AsNoTracking().Where(e => missingIds.Contains(e.Id)).ToListAsync();
        var summaries = sum.Rows
            .Concat(extra.Select(e => new MonthlySummaryRowDto(e.Id, e.Name, e.IsActive, 0, 0, 0, 0, 0, 0, 0, 0, 0, null)))
            .OrderBy(r => r.EmployeeName)
            .ToList();

        var rows = summaries
            .Select(r => bonuses.GetValueOrDefault(r.EmployeeId) is { } b
                ? new BonusRowDto(r, b.Amount, b.Note)
                : new BonusRowDto(r, null, null))
            .ToList();

        return new BonusMonthDto(
            year,
            month,
            BonusRules.MonthEnded(year, month, Today),
            monthLock is not null,
            monthLock is null ? null : DateTime.SpecifyKind(monthLock.LockedAt, DateTimeKind.Utc),
            monthLock?.LockedBy,
            s.BonusCap,
            rows.Sum(r => r.Amount ?? 0),
            rows);
    }

    public async Task<ServiceError?> SaveAsync(int year, int month, IReadOnlyList<BonusEntryRequest> entries)
    {
        if (await IsLockedAsync(year, month)) return LockedError;

        var ids = entries.Select(e => e.EmployeeId).ToList();
        if (await db.Employees.CountAsync(e => ids.Contains(e.Id)) != ids.Count)
            return new ServiceError("אחת העובדות לא נמצאה");

        var existing = await db.MonthlyBonuses
            .Where(b => b.Year == year && b.Month == month && ids.Contains(b.EmployeeId))
            .ToDictionaryAsync(b => b.EmployeeId);

        foreach (var entry in entries)
        {
            var row = existing.GetValueOrDefault(entry.EmployeeId);
            if (entry.Amount is not { } amount)
            {
                if (row is not null) db.MonthlyBonuses.Remove(row);
                continue;
            }
            if (row is null)
            {
                row = new MonthlyBonus { EmployeeId = entry.EmployeeId, Year = year, Month = month };
                db.MonthlyBonuses.Add(row);
            }
            row.Amount = amount;
            row.Note = string.IsNullOrWhiteSpace(entry.Note) ? null : entry.Note.Trim();
        }

        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// שולח למנהלת את טבלת הבונוסים עם אקסל, ורק אם המייל יצא נועל את החודש.
    /// כך כשל במייל לא משאיר חודש נעול שאף אחד לא קיבל.
    /// </summary>
    public async Task<ServiceError?> SendAndLockAsync(int year, int month, string username)
    {
        if (await IsLockedAsync(year, month)) return new ServiceError("החודש כבר נשלח לשכר ונעול", StatusCodes.Status409Conflict);
        if (!BonusRules.MonthEnded(year, month, Today))
            return new ServiceError("אפשר לשלוח לשכר רק אחרי שהחודש הסתיים");

        var managerEmail = (await settings.GetAsync()).ManagerEmail;
        if (string.IsNullOrWhiteSpace(managerEmail))
            return new ServiceError("לא הוגדר מייל מנהלת. יש להגדיר אותו במסך ההגדרות.");

        var bonuses = await GetAsync(year, month);
        if (bonuses.Rows.Count == 0) return new ServiceError("אין עובדות בחודש הזה");
        var missing = bonuses.Rows.Where(r => r.Amount is null).Select(r => r.Summary.EmployeeName).ToList();
        if (missing.Count > 0)
            return new ServiceError($"חסר סכום בונוס (אפשר גם 0) עבור: {string.Join(", ", missing)}");

        var error = await email.SendAsync(new EmailMessage(
            EmailType,
            [managerEmail],
            BonusRules.Subject(year, month),
            BonusRules.Html(bonuses, username),
            new DateOnly(year, month, 1),
            [new EmailAttachment(BonusRules.ExcelFileName(year, month), BonusRules.Excel(bonuses), MonthlySummaryExcel.ContentType)]));
        if (error is not null)
            return new ServiceError("שליחת המייל נכשלה, והחודש לא ננעל. אפשר לנסות שוב.", StatusCodes.Status502BadGateway);

        var now = DateTime.UtcNow;
        db.MonthLocks.Add(new MonthLock { Year = year, Month = month, LockedBy = username, LockedAt = now, SentAt = now });
        await db.SaveChangesAsync();
        return null;
    }

    private Task<bool> IsLockedAsync(int year, int month) =>
        db.MonthLocks.AnyAsync(l => l.Year == year && l.Month == month);

    private static readonly ServiceError LockedError =
        new("החודש נשלח לשכר ונעול, אי אפשר לשנות בונוסים", StatusCodes.Status409Conflict);
}
