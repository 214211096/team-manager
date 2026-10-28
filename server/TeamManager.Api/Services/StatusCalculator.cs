using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

public static class StatusCalculator
{
    /// <summary>
    /// אחוז = ביצוע / יעד × 100.
    /// חלש: מתחת ל-weakBelow. טוב: מ-weakBelow עד excellentAbove כולל. מעולה: מעל excellentAbove.
    /// </summary>
    public static (decimal Percent, Rating Rating) Calculate(
        int completed, int target, decimal weakBelow = 80, decimal excellentAbove = 90)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(target);
        ArgumentOutOfRangeException.ThrowIfNegative(completed);

        var percent = completed * 100m / target;
        var rating = percent < weakBelow ? Rating.Weak
            : percent <= excellentAbove ? Rating.Good
            : Rating.Excellent;

        // הסטטוס נקבע לפי האחוז המדויק; העיגול רק לתצוגה ולשמירה.
        return (Math.Round(percent, 2, MidpointRounding.AwayFromZero), rating);
    }
}
