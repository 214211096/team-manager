namespace TeamManager.Api.Entities;

public enum ScheduleKind
{
    /// <summary>היעדרות מתוכננת: נכנסת לנוכחות כברירת מחדל.</summary>
    Absence,

    /// <summary>יום עבודה חלקי: נוכחת, והיעד היומי מוקטן לפי WorkPercent.</summary>
    PartialDay,
}

/// <summary>רישום מראש בלוח השנה לעובדת, לטווח תאריכים (כולל שני הקצוות).</summary>
public class ScheduleEntry
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public ScheduleKind Kind { get; set; }

    /// <summary>סוג ההיעדרות; רק ב-Absence (Vacation / Sick / Other).</summary>
    public AttendanceStatus? AbsenceStatus { get; set; }

    /// <summary>חובה בהיעדרות מסוג Other. סיבה כללית בלבד.</summary>
    public string? Reason { get; set; }

    /// <summary>אחוז העבודה ביום חלקי (1-99); רק ב-PartialDay.</summary>
    public int? WorkPercent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
