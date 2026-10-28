namespace TeamManager.Api.Entities;

/// <summary>
/// עובדת בצוות. לפי האפיון נשמר שם בלבד, ללא פרטים מזהים.
/// עובדת עם נתונים לא נמחקת, רק מסומנת לא פעילה כדי לשמור היסטוריה.
/// </summary>
public class Employee
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DailyTarget { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
