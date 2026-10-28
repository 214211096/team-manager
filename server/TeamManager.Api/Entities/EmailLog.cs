namespace TeamManager.Api.Entities;

/// <summary>יומן של כל מייל שהמערכת ניסתה לשלוח.</summary>
public class EmailLog
{
    public int Id { get; set; }

    /// <summary>סוג המייל, למשל AttendanceReport / AttendanceReminder / MonthlySummary / BonusPayroll.</summary>
    public required string Type { get; set; }

    public required string To { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>היום שהמייל מתייחס אליו (דיווח או תזכורת נוכחות), כדי לדעת אם הדיווח של יום מסוים נשלח.</summary>
    public DateOnly? ForDate { get; set; }
}
