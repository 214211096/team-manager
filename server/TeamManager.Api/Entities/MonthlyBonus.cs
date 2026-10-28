namespace TeamManager.Api.Entities;

/// <summary>בונוס חודשי לעובדת. ייחודי לפי EmployeeId + Year + Month.</summary>
public class MonthlyBonus
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
