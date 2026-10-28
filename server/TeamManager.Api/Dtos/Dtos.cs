using System.ComponentModel.DataAnnotations;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Dtos;

public record LoginRequest(
    [Required] string Username,
    [Required] string Password);

public record ChangePasswordRequest(
    [Required(ErrorMessage = "חובה להזין את הסיסמה הנוכחית")] string CurrentPassword,
    [Required(ErrorMessage = "חובה להזין סיסמה חדשה")]
    [MinLength(10, ErrorMessage = "סיסמה חדשה של 10 תווים לפחות")]
    [MaxLength(200, ErrorMessage = "סיסמה עד 200 תווים")]
    string NewPassword);

public record CurrentUserDto(int Id, string Username, string? DisplayName, string Email, UserRole Role);

public record EmployeeDto(int Id, string Name, int DailyTarget, bool IsActive, DateTime CreatedAt)
{
    public static EmployeeDto From(Employee e) => new(e.Id, e.Name, e.DailyTarget, e.IsActive, e.CreatedAt);
}

public record EmployeeUpsertRequest(
    [Required(ErrorMessage = "חובה להזין שם")]
    [StringLength(100, ErrorMessage = "שם עד 100 תווים")]
    string Name,
    [Range(1, 10000, ErrorMessage = "יעד יומי בין 1 ל-10000")]
    int DailyTarget,
    bool IsActive = true);

/// <summary>מה מתוכנן בלוח השנה לעובדת ביום מסוים.</summary>
public record PlannedDayDto(ScheduleKind Kind, AttendanceStatus? AbsenceStatus, string? Reason, int? WorkPercent);

public record AttendanceRowDto(
    int EmployeeId,
    string EmployeeName,
    bool IsActive,
    AttendanceStatus? Status,
    string? OtherReason,
    PlannedDayDto? Planned = null);

/// <summary>נוכחות של יום אחד. ReportSentAt הוא מועד השליחה האחרונה למנהלת (UTC), או null.</summary>
public record AttendanceDayDto(DateOnly Date, bool Locked, DateTime? ReportSentAt, IReadOnlyList<AttendanceRowDto> Rows);

public record AttendanceEntryRequest(int EmployeeId, AttendanceStatus Status, string? OtherReason);

public record AttendanceSaveRequest([Required] List<AttendanceEntryRequest> Entries);

/// <summary>שורת ביצוע לעובדת שנוכחת באותו יום. Target הוא היעד שנשמר ביום הזה, או היעד הנוכחי אם עוד לא הוזן ביצוע.</summary>
public record DailyStatusRowDto(
    int EmployeeId,
    string EmployeeName,
    int Target,
    int? Completed,
    decimal? Percent,
    Rating? Rating,
    int WorkPercent = 100);

/// <summary>AttendanceCount: כמה עובדות דווחו בנוכחות באותו יום (0 = עוד לא דווחה נוכחות).</summary>
public record DailyStatusDayDto(
    DateOnly Date,
    bool Locked,
    decimal WeakThreshold,
    decimal ExcellentThreshold,
    int AttendanceCount,
    IReadOnlyList<DailyStatusRowDto> Rows);

/// <summary>Completed ריק מוחק את הביצוע של העובדת באותו יום.</summary>
public record DailyStatusEntryRequest(
    int EmployeeId,
    [Range(0, 100000, ErrorMessage = "ביצוע בין 0 ל-100000")] int? Completed);

public record DailyStatusSaveRequest([Required] List<DailyStatusEntryRequest> Entries);

public record ScheduleEntryDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    DateOnly StartDate,
    DateOnly EndDate,
    ScheduleKind Kind,
    AttendanceStatus? AbsenceStatus,
    string? Reason,
    int? WorkPercent);

public record ScheduleEntryRequest(
    int EmployeeId,
    DateOnly StartDate,
    DateOnly EndDate,
    ScheduleKind Kind,
    AttendanceStatus? AbsenceStatus,
    string? Reason,
    int? WorkPercent);

/// <summary>
/// סיכום חודשי לעובדת. ימים לפי רשומות נוכחות; סטטוסים וממוצע לפי ימים עם ביצוע בלבד
/// (ימי היעדרות לא נספרים בממוצע).
/// </summary>
public record MonthlySummaryRowDto(
    int EmployeeId,
    string EmployeeName,
    bool IsActive,
    int PresentDays,
    int PartialDays,
    int VacationDays,
    int SickDays,
    int OtherDays,
    int WeakDays,
    int GoodDays,
    int ExcellentDays,
    int DaysWithoutOutput,
    decimal? AveragePercent);

/// <summary>TeamAveragePercent: ממוצע על כל ימי הביצוע של הצוות. SentAt: שליחה אחרונה במייל (UTC).</summary>
public record MonthlySummaryDto(
    int Year,
    int Month,
    bool Locked,
    DateTime? SentAt,
    decimal? TeamAveragePercent,
    IReadOnlyList<MonthlySummaryRowDto> Rows);

/// <summary>שורת בונוס: נתוני הסיכום החודשי של העובדת, והסכום וההערה שמנהלת הצוות הזינה.</summary>
public record BonusRowDto(MonthlySummaryRowDto Summary, decimal? Amount, string? Note);

/// <summary>
/// בונוסים לחודש. Ended: החודש הסתיים ואפשר לשלוח לשכר.
/// LockedAt/LockedBy/SentAt ממולאים אחרי שליחה לשכר (UTC).
/// </summary>
public record BonusMonthDto(
    int Year,
    int Month,
    bool Ended,
    bool Locked,
    DateTime? LockedAt,
    string? LockedBy,
    decimal BonusCap,
    decimal Total,
    IReadOnlyList<BonusRowDto> Rows);

/// <summary>Amount ריק מוחק את הבונוס של העובדת לאותו חודש.</summary>
public record BonusEntryRequest(int EmployeeId, decimal? Amount, string? Note);

public record BonusSaveRequest([Required] List<BonusEntryRequest> Entries);

public class AppSettingsDto : IValidatableObject
{
    public string ManagerEmail { get; set; } = "";

    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "שעה בפורמט HH:mm")]
    public string ReminderTime { get; set; } = "";

    [Range(0, 100, ErrorMessage = "סף בין 0 ל-100")]
    public decimal WeakThreshold { get; set; }

    [Range(0, 100, ErrorMessage = "סף בין 0 ל-100")]
    public decimal ExcellentThreshold { get; set; }

    [Range(0, 100000, ErrorMessage = "תקרת בונוס לא תקינה")]
    public decimal BonusCap { get; set; }

    /// <summary>ימים (חגים) שבהם לא נשלחת תזכורת נוכחות.</summary>
    [MaxLength(80, ErrorMessage = "עד 80 ימים")]
    public List<DateOnly> NoReminderDates { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        // מייל המנהלת יכול להישאר ריק עד שמגדירים אותו, אבל אם הוזן הוא חייב להיות תקין.
        if (!string.IsNullOrWhiteSpace(ManagerEmail) && !new EmailAddressAttribute().IsValid(ManagerEmail))
            yield return new ValidationResult("כתובת מייל לא תקינה", [nameof(ManagerEmail)]);

        if (WeakThreshold > ExcellentThreshold)
            yield return new ValidationResult(
                "סף 'חלש' חייב להיות קטן או שווה לסף 'מעולה'",
                [nameof(WeakThreshold), nameof(ExcellentThreshold)]);
    }
}
