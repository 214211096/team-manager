using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Tests;

public class StatusCalculatorTests
{
    [Theory]
    [InlineData(0, 100, Rating.Weak)]
    [InlineData(79, 100, Rating.Weak)]
    [InlineData(80, 100, Rating.Good)]   // 80% בדיוק = טוב
    [InlineData(90, 100, Rating.Good)]   // 90% בדיוק = טוב (כולל)
    [InlineData(91, 100, Rating.Excellent)]
    [InlineData(150, 100, Rating.Excellent)]
    public void Rating_follows_thresholds(int completed, int target, Rating expected)
    {
        var (_, rating) = StatusCalculator.Calculate(completed, target);
        Assert.Equal(expected, rating);
    }

    [Fact]
    public void Just_below_80_is_weak_even_if_rounding_would_reach_80()
    {
        // 79995/100000 = 79.995% -> מוצג 80.00 אבל עדיין מתחת ל-80
        var (percent, rating) = StatusCalculator.Calculate(79995, 100000);
        Assert.Equal(80.00m, percent);
        Assert.Equal(Rating.Weak, rating);
    }

    [Fact]
    public void Just_above_90_is_excellent_even_when_rounded_percent_shows_90()
    {
        // 90001/100000 = 90.001% -> מוצג 90.00 אבל מעל 90
        var (percent, rating) = StatusCalculator.Calculate(90001, 100000);
        Assert.Equal(90.00m, percent);
        Assert.Equal(Rating.Excellent, rating);
    }

    [Fact]
    public void Percent_is_rounded_to_two_decimals()
    {
        var (percent, _) = StatusCalculator.Calculate(2, 3);
        Assert.Equal(66.67m, percent);
    }

    [Fact]
    public void Custom_thresholds_are_respected()
    {
        var (_, rating) = StatusCalculator.Calculate(75, 100, weakBelow: 70, excellentAbove: 74);
        Assert.Equal(Rating.Excellent, rating);
    }

    [Fact]
    public void Zero_target_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatusCalculator.Calculate(5, 0));
    }
}
