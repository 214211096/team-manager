namespace TeamManager.Api.Entities;

/// <summary>
/// חודש שנשלח לשכר. קיום רשומה חוסם עריכה של נוכחות, ביצוע ובונוס לאותו חודש.
/// </summary>
public class MonthLock
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime LockedAt { get; set; } = DateTime.UtcNow;
    public required string LockedBy { get; set; }
    public DateTime? SentAt { get; set; }
}
