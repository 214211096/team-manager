using TeamManager.Api.Services;

namespace TeamManager.Tests;

public class HolidayCalendarTests
{
    [Theory]
    [InlineData(2026, 4, 2, "פסח")]
    [InlineData(2026, 4, 8, "שביעי של פסח")]
    [InlineData(2026, 4, 22, "יום העצמאות")]
    [InlineData(2026, 5, 22, "שבועות")]
    [InlineData(2026, 9, 12, "ראש השנה")]
    [InlineData(2026, 9, 13, "ראש השנה")]
    [InlineData(2026, 9, 21, "יום כיפור")]
    [InlineData(2026, 9, 26, "סוכות")]
    [InlineData(2026, 10, 3, "שמחת תורה")]
    [InlineData(2027, 4, 22, "פסח")] // שנה מעוברת (תשפ"ז)
    public void Known_holidays(int year, int month, int day, string name) =>
        Assert.Equal(name, HolidayCalendar.NameOf(new DateOnly(year, month, day)));

    [Theory]
    [InlineData(2024, 5, 14)] // ה' באייר ביום שני: נדחה לשלישי
    [InlineData(2025, 5, 1)] // ה' באייר בשישי: מוקדם לחמישי
    public void Independence_day_moves_by_law(int year, int month, int day) =>
        Assert.Equal("יום העצמאות", HolidayCalendar.NameOf(new DateOnly(year, month, day)));

    [Fact]
    public void Weekends_and_holidays_are_not_workdays()
    {
        Assert.True(HolidayCalendar.IsWorkday(new DateOnly(2026, 10, 28))); // רביעי רגיל
        Assert.False(HolidayCalendar.IsWorkday(new DateOnly(2026, 10, 30))); // שישי
        Assert.False(HolidayCalendar.IsWorkday(new DateOnly(2026, 10, 31))); // שבת
        Assert.False(HolidayCalendar.IsWorkday(new DateOnly(2026, 9, 21))); // יום כיפור, שני
        Assert.True(HolidayCalendar.IsWorkday(new DateOnly(2026, 9, 28))); // חול המועד
    }

    [Fact]
    public void Between_spans_years()
    {
        var list = HolidayCalendar.Between(new DateOnly(2026, 9, 1), new DateOnly(2027, 4, 30));
        Assert.Equal(["ראש השנה", "ראש השנה", "יום כיפור", "סוכות", "שמחת תורה", "פסח", "שביעי של פסח"],
            list.Select(h => h.Name));
    }
}
