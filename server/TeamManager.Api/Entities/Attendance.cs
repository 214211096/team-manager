namespace TeamManager.Api.Entities;

public enum AttendanceStatus
{
    Present,
    Vacation,
    Sick,
    Other,
}

/// <summary>רשומת נוכחות אחת לעובדת ביום. ייחודי לפי EmployeeId + Date.</summary>
public class Attendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly Date { get; set; }
    public AttendanceStatus Status { get; set; }

    /// <summary>חובה רק כש-Status הוא Other. סיבה כללית בלבד.</summary>
    public string? OtherReason { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
