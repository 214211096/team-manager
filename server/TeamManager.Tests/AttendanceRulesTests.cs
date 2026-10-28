using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Tests;

public class AttendanceRulesTests
{
    private static AttendanceEntryRequest Entry(int id, AttendanceStatus status, string? reason = null) =>
        new(id, status, reason);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Other_requires_a_reason(string? reason)
    {
        var errors = AttendanceRules.Validate([Entry(1, AttendanceStatus.Other, reason)]);
        Assert.Contains("entries[0].otherReason", errors.Keys);
    }

    [Fact]
    public void Other_with_reason_is_valid()
    {
        Assert.Empty(AttendanceRules.Validate([Entry(1, AttendanceStatus.Other, "תור לרופא")]));
    }

    [Theory]
    [InlineData(AttendanceStatus.Present)]
    [InlineData(AttendanceStatus.Vacation)]
    [InlineData(AttendanceStatus.Sick)]
    public void Other_statuses_do_not_need_a_reason(AttendanceStatus status)
    {
        Assert.Empty(AttendanceRules.Validate([Entry(1, status)]));
    }

    [Fact]
    public void Reason_longer_than_limit_is_rejected()
    {
        var reason = new string('א', AttendanceRules.OtherReasonMaxLength + 1);
        var errors = AttendanceRules.Validate([Entry(1, AttendanceStatus.Other, reason)]);
        Assert.Contains("entries[0].otherReason", errors.Keys);
    }

    [Fact]
    public void Same_employee_twice_is_rejected()
    {
        var errors = AttendanceRules.Validate([Entry(1, AttendanceStatus.Present), Entry(1, AttendanceStatus.Sick)]);
        Assert.Contains("entries", errors.Keys);
    }

    [Fact]
    public void Undefined_status_number_is_rejected()
    {
        // JSON מקבל גם מספרים לסטטוס, אז ערך מחוץ ל-enum חייב להיחסם.
        var errors = AttendanceRules.Validate([Entry(1, (AttendanceStatus)99)]);
        Assert.Contains("entries[0].status", errors.Keys);
    }

    [Theory]
    [InlineData("2026-10-25", true)]  // ראשון
    [InlineData("2026-10-29", true)]  // חמישי
    [InlineData("2026-10-30", false)] // שישי
    [InlineData("2026-10-31", false)] // שבת
    public void Workdays_are_sunday_to_thursday(string date, bool expected)
    {
        Assert.Equal(expected, AttendanceRules.IsWorkday(DateOnly.Parse(date)));
    }

    [Theory]
    [InlineData("2026-10-27 09:29", false)]
    [InlineData("2026-10-27 09:30", true)]
    [InlineData("2026-10-27 15:00", true)]
    [InlineData("2026-10-30 10:00", false)] // שישי
    public void Reminder_is_due_from_reminder_time_on_workdays(string now, bool expected)
    {
        Assert.Equal(expected, AttendanceRules.IsReminderDue(DateTime.Parse(now), new TimeOnly(9, 30), []));
    }

    [Fact]
    public void No_reminder_on_configured_holiday()
    {
        var holiday = new DateOnly(2026, 10, 27);
        Assert.False(AttendanceRules.IsReminderDue(new DateTime(2026, 10, 27, 10, 0, 0), new TimeOnly(9, 30), [holiday]));
        Assert.True(AttendanceRules.IsReminderDue(new DateTime(2026, 10, 28, 10, 0, 0), new TimeOnly(9, 30), [holiday]));
    }

    [Fact]
    public void No_reminder_on_automatic_holiday()
    {
        // יום כיפור תשפ"ז: יום שני, בלי הגדרה ידנית.
        Assert.False(AttendanceRules.IsReminderDue(new DateTime(2026, 9, 21, 10, 0, 0), new TimeOnly(9, 30), []));
    }

    private static readonly DateOnly Day = new(2026, 10, 27);

    [Fact]
    public void Report_html_encodes_names_and_reasons()
    {
        AttendanceRowDto[] rows = [new(1, "<script>", true, AttendanceStatus.Other, "a & b")];

        var html = AttendanceRules.ReportHtml(Day, rows, changed: null);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("a &amp; b", html);
    }

    [Fact]
    public void Report_shows_present_count_and_lists_only_absent_with_reason()
    {
        AttendanceRowDto[] rows =
        [
            new(1, "רחל", true, AttendanceStatus.Present, null),
            new(2, "שרה", true, AttendanceStatus.Present, null),
            new(3, "לאה", true, AttendanceStatus.Sick, null),
            new(4, "דינה", true, AttendanceStatus.Other, "תור לרופא"),
        ];

        var html = AttendanceRules.ReportHtml(Day, rows, changed: null);

        Assert.Contains("נוכחות: 2 מתוך 4", html);
        Assert.Contains("<td>לאה</td><td>מחלה</td>", html);
        Assert.Contains("<td>דינה</td><td>אחר – תור לרופא</td>", html);
        Assert.DoesNotContain("רחל", html);
        Assert.DoesNotContain("עדכון", html);
    }

    [Fact]
    public void Report_with_everyone_present_says_so()
    {
        var html = AttendanceRules.ReportHtml(Day, [new(1, "רחל", true, AttendanceStatus.Present, null)], changed: null);
        Assert.Contains("כל העובדות נוכחות", html);
    }

    [Fact]
    public void Update_report_lists_what_changed()
    {
        AttendanceRowDto[] rows =
        [
            new(1, "רחל", true, AttendanceStatus.Sick, null),
            new(2, "שרה", true, AttendanceStatus.Present, null),
        ];

        var html = AttendanceRules.ReportHtml(Day, rows, changed: [rows[0]]);

        Assert.Contains("עדכון לדיווח", html);
        Assert.Contains("<li>רחל: מחלה</li>", html);
    }

    [Fact]
    public void Dates_use_day_month_year_with_slashes()
    {
        Assert.EndsWith("27/10/2026", AttendanceRules.FormatDate(Day));
    }

    [Fact]
    public void Resent_report_is_marked_as_update()
    {
        Assert.StartsWith("עדכון:", AttendanceRules.ReportSubject(Day, isUpdate: true));
        Assert.DoesNotContain("עדכון", AttendanceRules.ReportSubject(Day, isUpdate: false));
    }
}

public class ZonedTimeProviderTests
{
    [Fact]
    public void Local_time_follows_configured_zone_not_server()
    {
        // 22:30 UTC בקיץ = 01:30 למחרת בישראל: "היום" חייב להיות לפי ישראל.
        var israel = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
        var utc = new DateTimeOffset(2026, 7, 1, 22, 30, 0, TimeSpan.Zero);

        var local = TimeZoneInfo.ConvertTime(utc, new ZonedTimeProvider(israel).LocalTimeZone);

        Assert.Equal(new DateTime(2026, 7, 2, 1, 30, 0), local.DateTime);
    }
}
