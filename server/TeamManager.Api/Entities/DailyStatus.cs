namespace TeamManager.Api.Entities;

public enum Rating
{
    Weak,
    Good,
    Excellent,
}

/// <summary>
/// ביצוע יומי מול יעד. היעד נשמר ברשומה, כך ששינוי יעד של עובדת לא משנה נתוני עבר.
/// BaseTarget הוא היעד המלא של העובדת ביום ההזנה; TargetSnapshot הוא היעד בפועל לאותו יום
/// (מוקטן ביום חלקי לפי WorkPercent), ולפיו מחושב האחוז.
/// </summary>
public class DailyStatus
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int BaseTarget { get; set; }
    public int WorkPercent { get; set; } = 100;
    public int TargetSnapshot { get; set; }
    public int Completed { get; set; }
    public decimal Percent { get; set; }
    public Rating Rating { get; set; }
}
