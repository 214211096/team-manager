namespace TeamManager.Api.Entities;

/// <summary>
/// עובדת בצוות. לפי האפיון נשמר שם בלבד, ללא פרטים מזהים.
/// מחיקה היא רכה: העובדת עוברת לארכיון (DeletedAt) עם כל ההיסטוריה ואפשר לשחזר.
/// </summary>
public class Employee
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DailyTarget { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>מתי הועברה לארכיון. null = לא נמחקה.</summary>
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
