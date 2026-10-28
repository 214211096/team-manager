namespace TeamManager.Api.Entities;

public enum UserRole
{
    TeamLead,
    Manager,
}

/// <summary>משתמשת מערכת: מנהלת צוות או מנהלת. העובדות לא נכנסות למערכת.</summary>
public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    /// <summary>השם שמוצג במערכת (למשל "חנה"). אם ריק, מוצג שם המשתמש.</summary>
    public string? DisplayName { get; set; }
    public string PasswordHash { get; set; } = "";
    public required string Email { get; set; }
    public UserRole Role { get; set; }
}
