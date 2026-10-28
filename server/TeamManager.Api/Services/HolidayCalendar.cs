using System.Globalization;

namespace TeamManager.Api.Services;

public record Holiday(DateOnly Date, string Name);

/// <summary>
/// חגי ישראל שבהם לא עובדים, מחושבים מהלוח העברי (בלי שירות חיצוני):
/// ראש השנה (יומיים), יום כיפור, סוכות, שמחת תורה, פסח, שביעי של פסח, יום העצמאות ושבועות.
/// ערבי חג וחול המועד נחשבים ימי עבודה; ימים נוספים מגדירים בהגדרות.
/// </summary>
public static class HolidayCalendar
{
    private static readonly HebrewCalendar Hebrew = new();

    /// <summary>ימי עבודה: ראשון עד חמישי, שאינם חג.</summary>
    public static bool IsWorkday(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday) && NameOf(date) is null;

    public static string? NameOf(DateOnly date) =>
        ForYear(date.Year).FirstOrDefault(h => h.Date == date)?.Name;

    public static IReadOnlyList<Holiday> Between(DateOnly from, DateOnly to) =>
        Enumerable.Range(from.Year, to.Year - from.Year + 1)
            .SelectMany(ForYear)
            .Where(h => h.Date >= from && h.Date <= to)
            .ToList();

    /// <summary>
    /// החגים בשנה לועזית: חגי האביב שייכים לשנה העברית שהתחילה בסתיו הקודם (Y+3760),
    /// וחגי תשרי לשנה העברית שמתחילה בסתיו (Y+3761).
    /// </summary>
    public static IReadOnlyList<Holiday> ForYear(int year)
    {
        var spring = year + 3760;
        var autumn = year + 3761;
        // בשנה מעוברת יש אדר א' ואדר ב', ולכן ניסן הוא החודש ה-8 ולא ה-7.
        var nisan = Hebrew.IsLeapYear(spring) ? 8 : 7;

        return
        [
            new(Date(spring, nisan, 15), "פסח"),
            new(Date(spring, nisan, 21), "שביעי של פסח"),
            new(IndependenceDay(spring, nisan + 1), "יום העצמאות"),
            new(Date(spring, nisan + 2, 6), "שבועות"),
            new(Date(autumn, 1, 1), "ראש השנה"),
            new(Date(autumn, 1, 2), "ראש השנה"),
            new(Date(autumn, 1, 10), "יום כיפור"),
            new(Date(autumn, 1, 15), "סוכות"),
            new(Date(autumn, 1, 22), "שמחת תורה"),
        ];
    }

    /// <summary>
    /// ה' באייר, עם ההזזות שנקבעו בחוק: שישי או שבת מוקדם לחמישי, שני נדחה לשלישי.
    /// </summary>
    private static DateOnly IndependenceDay(int hebrewYear, int iyar)
    {
        var date = Date(hebrewYear, iyar, 5);
        return date.DayOfWeek switch
        {
            DayOfWeek.Friday => date.AddDays(-1),
            DayOfWeek.Saturday => date.AddDays(-2),
            DayOfWeek.Monday => date.AddDays(1),
            _ => date,
        };
    }

    private static DateOnly Date(int year, int month, int day) =>
        DateOnly.FromDateTime(Hebrew.ToDateTime(year, month, day, 0, 0, 0, 0));
}
