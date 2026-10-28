using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Tests;

public class MonthlySummaryRulesTests
{
    private static readonly Employee Rachel = new() { Id = 1, Name = "רחל", DailyTarget = 50 };
    private static readonly Employee Sara = new() { Id = 2, Name = "שרה", DailyTarget = 40 };
    private static readonly Employee Left = new() { Id = 3, Name = "עזבה", DailyTarget = 40, IsActive = false };

    private static DateOnly D(int day) => new(2026, 10, day);

    private static Attendance Att(Employee e, int day, AttendanceStatus s) =>
        new() { EmployeeId = e.Id, Date = D(day), Status = s, OtherReason = s == AttendanceStatus.Other ? "x" : null };

    private static DailyStatus Status(Employee e, int day, decimal percent, Rating rating, int workPercent = 100) =>
        new() { EmployeeId = e.Id, Date = D(day), Percent = percent, Rating = rating, WorkPercent = workPercent };

    [Fact]
    public void Counts_days_by_attendance_and_rating()
    {
        Attendance[] attendance =
        [
            Att(Rachel, 1, AttendanceStatus.Present), Att(Rachel, 2, AttendanceStatus.Present),
            Att(Rachel, 3, AttendanceStatus.Present), Att(Rachel, 4, AttendanceStatus.Vacation),
            Att(Rachel, 5, AttendanceStatus.Sick), Att(Rachel, 6, AttendanceStatus.Other),
        ];
        DailyStatus[] statuses =
        [
            Status(Rachel, 1, 70, Rating.Weak),
            Status(Rachel, 2, 100, Rating.Excellent, workPercent: 50),
            // יום 3: נוכחת בלי ביצוע
        ];

        var row = Assert.Single(MonthlySummaryRules.BuildRows([Rachel], attendance, statuses));

        Assert.Equal(3, row.PresentDays);
        Assert.Equal(1, row.PartialDays);
        Assert.Equal((1, 1, 1), (row.VacationDays, row.SickDays, row.OtherDays));
        Assert.Equal((1, 0, 1), (row.WeakDays, row.GoodDays, row.ExcellentDays));
        Assert.Equal(1, row.DaysWithoutOutput);
        Assert.Equal(85.00m, row.AveragePercent); // (70 + 100) / 2: יום בלי ביצוע וימי היעדרות לא נספרים
    }

    [Fact]
    public void Daily_output_counts_only_present_days()
    {
        Attendance[] attendance = [Att(Rachel, 1, AttendanceStatus.Present), Att(Rachel, 2, AttendanceStatus.Sick)];
        DailyStatus[] statuses =
        [
            new() { EmployeeId = Rachel.Id, Date = D(1), Completed = 40 },
            new() { EmployeeId = Rachel.Id, Date = D(2), Completed = 50 },
        ];

        var daily = MonthlySummaryRules.DailyOutput(attendance, statuses);

        Assert.Equal(new Dictionary<int, int> { [1] = 40 }, daily[Rachel.Id]);
    }

    [Fact]
    public void Output_on_a_day_marked_absent_is_ignored()
    {
        // לא אמור לקרות (סימון היעדרות מוחק ביצוע), אבל אם יש נתון ישן הוא לא נספר.
        var row = Assert.Single(MonthlySummaryRules.BuildRows(
            [Rachel], [Att(Rachel, 1, AttendanceStatus.Sick)], [Status(Rachel, 1, 100, Rating.Excellent)]));

        Assert.Equal(0, row.ExcellentDays);
        Assert.Null(row.AveragePercent);
    }

    [Fact]
    public void Inactive_employee_appears_only_with_data()
    {
        var rows = MonthlySummaryRules.BuildRows([Rachel, Left], [], []);
        Assert.Equal(["רחל"], rows.Select(r => r.EmployeeName));

        rows = MonthlySummaryRules.BuildRows([Rachel, Left], [Att(Left, 1, AttendanceStatus.Present)], []);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Team_average_is_over_all_days_not_average_of_averages()
    {
        Attendance[] attendance =
        [
            Att(Rachel, 1, AttendanceStatus.Present),
            Att(Sara, 1, AttendanceStatus.Present), Att(Sara, 2, AttendanceStatus.Present), Att(Sara, 3, AttendanceStatus.Present),
        ];
        DailyStatus[] statuses =
        [
            Status(Rachel, 1, 60, Rating.Weak),
            Status(Sara, 1, 100, Rating.Excellent), Status(Sara, 2, 100, Rating.Excellent), Status(Sara, 3, 100, Rating.Excellent),
        ];

        Assert.Equal(90.00m, MonthlySummaryRules.TeamAverage(statuses, attendance)); // 360 / 4, לא (60+100)/2
    }

    [Theory]
    [InlineData("2026-11-01 07:59", false)]
    [InlineData("2026-11-01 08:00", true)]
    [InlineData("2026-11-02 06:00", true)] // השרת לא רץ ב-1 לחודש: שולחים כשהוא עולה
    public void Summary_is_due_from_the_first_at_eight(string now, bool expected)
    {
        Assert.Equal(expected, MonthlySummaryRules.IsSummaryDue(DateTime.Parse(now)));
    }

    [Fact]
    public void Previous_month_of_january_is_december_of_last_year()
    {
        Assert.Equal((2025, 12), MonthlySummaryRules.PreviousMonth(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Email_encodes_names_and_includes_link()
    {
        var s = new MonthlySummaryDto(2026, 10, false, null, 90m,
            [new(1, "<b>", true, 1, 0, 0, 0, 0, 0, 0, 1, 0, 90m)]);

        var html = MonthlySummaryRules.Html(s, "http://x/monthly?month=2026-10");

        Assert.Contains("&lt;b&gt;", html);
        Assert.Contains("href=\"http://x/monthly?month=2026-10\"", html);
        Assert.Contains("אוקטובר 2026", html);
    }

    [Fact]
    public void Excel_is_right_to_left_with_a_row_per_employee_and_team_row()
    {
        var s = new MonthlySummaryDto(2026, 10, false, null, 85m,
        [
            new(1, "רחל", true, 3, 1, 1, 0, 0, 1, 0, 1, 1, 85m),
            new(2, "שרה", true, 2, 0, 0, 1, 0, 0, 2, 0, 0, 85m),
        ]);

        var daily = new Dictionary<int, Dictionary<int, int>>
        {
            [1] = new() { [1] = 40, [2] = 55 },
            [2] = new() { [1] = 30 },
        };

        using var workbook = new XLWorkbook(new MemoryStream(MonthlySummaryExcel.Build(s, daily)));
        var ws = workbook.Worksheet(1);

        Assert.True(ws.RightToLeft);
        Assert.Equal("רחל", ws.Cell(4, 1).GetString());
        Assert.Equal("שרה", ws.Cell(5, 1).GetString());
        Assert.Equal("סה״כ צוות", ws.Cell(6, 1).GetString());
        Assert.Equal(5, ws.Cell(6, 2).GetValue<int>()); // סכום ימי נוכחות
        Assert.Equal(85m, ws.Cell(6, 8).GetValue<decimal>());

        // עמודה לכל יום (אוקטובר: 31 ימים מעמודה 9), ובסוף סה״כ.
        Assert.Equal("1/10", ws.Cell(3, 9).GetString());
        Assert.Equal("31/10", ws.Cell(3, 39).GetString());
        Assert.Equal("סה״כ הספק", ws.Cell(3, 40).GetString());
        Assert.Equal(55, ws.Cell(4, 10).GetValue<int>());
        Assert.True(ws.Cell(5, 10).IsEmpty());
        Assert.Equal(95, ws.Cell(4, 40).GetValue<int>());
        Assert.Equal(70, ws.Cell(6, 9).GetValue<int>()); // יום 1 לכל הצוות
        Assert.Equal(125, ws.Cell(6, 40).GetValue<int>());
        Assert.DoesNotContain(Enumerable.Range(1, 40), c => ws.Cell(3, c).GetString() == "ימים מעולה");
    }
}

public class MonthlySummaryServiceTests
{
    private readonly AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private readonly RecordingEmail email;

    public MonthlySummaryServiceTests() => email = new RecordingEmail(db);

    private MonthlySummaryService Service => new(db, email,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:BaseUrl"] = "http://app" }).Build());

    private void Seed(DateOnly attendanceDate)
    {
        var e = new Employee { Name = "רחל", DailyTarget = 50 };
        db.Employees.Add(e);
        db.Users.Add(new User { Username = "teamlead", Email = "lead@example.com", Role = UserRole.TeamLead });
        db.Users.Add(new User { Username = "manager", Email = "manager@example.com", Role = UserRole.Manager });
        db.SaveChanges();
        db.Attendance.Add(new Attendance { EmployeeId = e.Id, Date = attendanceDate, Status = AttendanceStatus.Present });
        db.SaveChanges();
    }

    [Fact]
    public async Task Sends_previous_month_once_to_team_lead_with_excel()
    {
        Seed(new DateOnly(2026, 10, 15));
        var now = new DateTime(2026, 11, 1, 8, 0, 0);

        await Service.SendIfDueAsync(now, default);
        await Service.SendIfDueAsync(now.AddMinutes(1), default);

        var sent = Assert.Single(email.Sent);
        Assert.Equal(["lead@example.com"], sent.To);
        Assert.Contains("אוקטובר 2026", sent.Subject);
        Assert.Equal("monthly-summary-2026-10.xlsx", Assert.Single(sent.Attachments!).FileName);
        Assert.Contains("http://app/bonuses?month=2026-10", sent.HtmlBody);
    }

    [Fact]
    public async Task Does_not_send_before_eight_on_the_first()
    {
        Seed(new DateOnly(2026, 10, 15));
        await Service.SendIfDueAsync(new DateTime(2026, 11, 1, 7, 59, 0), default);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Does_not_send_a_month_without_any_attendance()
    {
        Seed(new DateOnly(2026, 11, 1)); // נתונים רק מהחודש הנוכחי
        await Service.SendIfDueAsync(new DateTime(2026, 11, 1, 9, 0, 0), default);
        Assert.Empty(email.Sent);
    }

    /// <summary>שולח "מדומה" שגם רושם ב-EmailLog, כמו השולח האמיתי.</summary>
    private class RecordingEmail(AppDbContext db) : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public async Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            db.EmailLogs.Add(new EmailLog { Type = message.Type, To = string.Join(", ", message.To), Success = true, ForDate = message.ForDate });
            await db.SaveChangesAsync(ct);
            return null;
        }
    }
}
