using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Tests;

public class BonusRulesTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(600, true)]
    [InlineData(250.5, true)]
    [InlineData(601, false)] // תרחיש קבלה מהאפיון
    [InlineData(-1, false)]
    [InlineData(10.555, false)]
    public void Amount_is_between_zero_and_cap(decimal amount, bool valid)
    {
        var errors = BonusRules.Validate([new(1, amount, null)], cap: 600);
        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public void Cap_comes_from_settings()
    {
        Assert.NotEmpty(BonusRules.Validate([new(1, 500, null)], cap: 400));
    }

    [Theory]
    [InlineData("2026-10-31", false)]
    [InlineData("2026-11-01", true)]
    public void Month_can_be_sent_only_after_it_ended(string today, bool expected)
    {
        Assert.Equal(expected, BonusRules.MonthEnded(2026, 10, DateOnly.Parse(today)));
    }

    [Fact]
    public void Payroll_email_has_amounts_total_and_encodes_names()
    {
        var summary = new MonthlySummaryRowDto(1, "<i>", true, 20, 0, 0, 0, 0, 0, 0, 0, 0, null);
        var b = new BonusMonthDto(2026, 10, true, false, null, null, 600, 1100,
            [new(summary, 500, "a & b"), new(summary with { EmployeeId = 2, EmployeeName = "שרה" }, 600, null)]);

        var html = BonusRules.Html(b, "teamlead");

        Assert.Contains("&lt;i&gt;", html);
        Assert.Contains("a &amp; b", html);
        Assert.Contains("1,100 ש״ח", html);
    }
}

public class BonusServiceTests
{
    private readonly AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeEmail email = new();

    private BonusService Service
    {
        get
        {
            var settings = new SettingsService(db);
            var config = new ConfigurationBuilder().Build();
            return new BonusService(db, settings, new MonthlySummaryService(db, email, config), email, clock);
        }
    }

    private async Task<Employee> SeedAsync()
    {
        await new SettingsService(db).SaveAsync(new AppSettingsDto
        {
            ManagerEmail = "boss@example.com", ReminderTime = "09:30", WeakThreshold = 80, ExcellentThreshold = 90, BonusCap = 600,
        });
        var e = new Employee { Name = "רחל", DailyTarget = 50 };
        db.Employees.Add(e);
        await db.SaveChangesAsync();
        db.Attendance.Add(new Attendance { EmployeeId = e.Id, Date = new DateOnly(2026, 10, 15), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();
        return e;
    }

    [Fact]
    public async Task Sending_emails_the_manager_and_locks_the_month()
    {
        var e = await SeedAsync();
        await Service.SaveAsync(2026, 10, [new(e.Id, 400, null)]);

        Assert.Null(await Service.SendAndLockAsync(2026, 10, "teamlead"));

        var sent = Assert.Single(email.Sent);
        Assert.Equal(["boss@example.com"], sent.To);
        Assert.Equal("bonuses-2026-10.xlsx", Assert.Single(sent.Attachments!).FileName);
        var monthLock = await db.MonthLocks.SingleAsync();
        Assert.Equal("teamlead", monthLock.LockedBy);
        Assert.NotNull(monthLock.SentAt);
    }

    [Fact]
    public async Task Locked_month_cannot_be_edited()
    {
        var e = await SeedAsync();
        await Service.SaveAsync(2026, 10, [new(e.Id, 400, null)]);
        await Service.SendAndLockAsync(2026, 10, "teamlead");

        var error = await Service.SaveAsync(2026, 10, [new(e.Id, 500, null)]);

        Assert.Equal(409, error?.StatusCode);
        Assert.Equal(400, (await db.MonthlyBonuses.SingleAsync()).Amount);
        // אותה נעילה חוסמת גם נוכחות (הבדיקה במסך הנוכחות משתמשת ב-IsLockedAsync).
        var attendance = new AttendanceService(db, new SettingsService(db), email, new ScheduleService(db, new SettingsService(db)));
        Assert.True(await attendance.IsLockedAsync(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public async Task Failed_email_does_not_lock()
    {
        var e = await SeedAsync();
        await Service.SaveAsync(2026, 10, [new(e.Id, 400, null)]);
        email.Fail = true;

        var error = await Service.SendAndLockAsync(2026, 10, "teamlead");

        Assert.Equal(502, error?.StatusCode);
        Assert.Empty(db.MonthLocks);
    }

    [Fact]
    public async Task Every_employee_needs_an_amount_before_sending()
    {
        await SeedAsync();

        var error = await Service.SendAndLockAsync(2026, 10, "teamlead");

        Assert.Contains("רחל", error?.Message);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Current_month_cannot_be_sent()
    {
        var e = await SeedAsync();
        db.Attendance.Add(new Attendance { EmployeeId = e.Id, Date = new DateOnly(2026, 11, 1), Status = AttendanceStatus.Present });
        await db.SaveChangesAsync();
        await Service.SaveAsync(2026, 11, [new(e.Id, 400, null)]);

        Assert.NotNull(await Service.SendAndLockAsync(2026, 11, "teamlead"));
        Assert.Empty(db.MonthLocks);
    }

    [Fact]
    public async Task Rows_show_summary_and_total()
    {
        var e = await SeedAsync();
        var other = new Employee { Name = "שרה", DailyTarget = 40 };
        db.Employees.Add(other);
        await db.SaveChangesAsync();
        await Service.SaveAsync(2026, 10, [new(e.Id, 400, "מצוינת"), new(other.Id, 150.5m, null)]);

        var b = await Service.GetAsync(2026, 10);

        Assert.Equal(550.5m, b.Total);
        Assert.Equal(1, b.Rows.Single(r => r.Summary.EmployeeId == e.Id).Summary.PresentDays);
        Assert.Equal("מצוינת", b.Rows.Single(r => r.Summary.EmployeeId == e.Id).Note);
        Assert.True(b.Ended);
    }

    private class FakeEmail : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (Fail) return Task.FromResult<string?>("smtp down");
            Sent.Add(message);
            return Task.FromResult<string?>(null);
        }
    }
}
