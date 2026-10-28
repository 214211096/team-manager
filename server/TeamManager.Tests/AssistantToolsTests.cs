using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using TeamManager.Api.Data;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Tests;

public class AssistantToolsTests
{
    private readonly AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private AssistantTools Tools
    {
        get
        {
            var settings = new SettingsService(db);
            var email = new NoEmail();
            var summary = new MonthlySummaryService(db, email, new ConfigurationBuilder().Build());
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
            return new AssistantTools(db, summary, new BonusService(db, settings, summary, email, clock), new ScheduleService(db, settings));
        }
    }

    private static Dictionary<string, JsonElement> Input(object o) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(o))!;

    private async Task SeedAsync()
    {
        var ruth = new Employee { Name = "רות שטרן", DailyTarget = 120 };
        var sara = new Employee { Name = "שרה", DailyTarget = 40 };
        db.Employees.AddRange(ruth, sara);
        await db.SaveChangesAsync();
        db.Attendance.AddRange(
            new Attendance { EmployeeId = ruth.Id, Date = new DateOnly(2026, 10, 5), Status = AttendanceStatus.Sick },
            new Attendance { EmployeeId = ruth.Id, Date = new DateOnly(2026, 10, 6), Status = AttendanceStatus.Present },
            new Attendance { EmployeeId = ruth.Id, Date = new DateOnly(2026, 10, 7), Status = AttendanceStatus.Other, OtherReason = "השתלמות" },
            new Attendance { EmployeeId = sara.Id, Date = new DateOnly(2026, 10, 5), Status = AttendanceStatus.Present });
        db.MonthlyBonuses.Add(new MonthlyBonus { EmployeeId = sara.Id, Year = 2026, Month = 4, Amount = 450, Note = "מצוינת" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Attendance_filters_by_partial_name_and_range()
    {
        await SeedAsync();

        var json = await Tools.ExecuteAsync("get_attendance", Input(new { from = "2026-10-01", to = "2026-10-31", employee_name = "רות" }));

        var rows = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal("רות שטרן", r.GetProperty("employee").GetString()));
        Assert.Contains(rows, r => r.GetProperty("status").GetString() == "Sick" && r.GetProperty("date").GetString() == "2026-10-05");
        Assert.Contains("השתלמות", json); // עברית לא מקודדת, כדי לחסוך טוקנים
    }

    [Fact]
    public async Task Bonuses_show_amount_for_month()
    {
        await SeedAsync();

        var json = await Tools.ExecuteAsync("get_bonuses", Input(new { year = 2026, month = 4 }));

        var root = JsonDocument.Parse(json).RootElement;
        var sara = root.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employee").GetString() == "שרה");
        Assert.Equal(450, sara.GetProperty("amount").GetDecimal());
        Assert.Equal(450, root.GetProperty("total").GetDecimal());
    }

    [Theory]
    [InlineData("get_attendance", "{\"from\":\"2026-10-31\",\"to\":\"2026-10-01\"}")]
    [InlineData("get_attendance", "{\"from\":\"01/10/2026\",\"to\":\"2026-10-31\"}")]
    [InlineData("get_attendance", "{\"from\":\"2025-01-01\",\"to\":\"2026-10-31\"}")] // יותר משנה
    [InlineData("get_bonuses", "{\"year\":2026,\"month\":13}")]
    [InlineData("delete_everything", "{}")]
    public async Task Bad_input_returns_error_for_the_model_instead_of_throwing(string tool, string input)
    {
        var json = await Tools.ExecuteAsync(tool, JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(input)!);
        Assert.True(JsonDocument.Parse(json).RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void Every_tool_has_a_valid_schema()
    {
        foreach (var d in AssistantTools.Definitions)
        {
            Assert.Matches("^[a-z_]+$", d.Name);
            Assert.All(d.Required, r => Assert.Contains(r, d.Properties.Keys));
        }
    }

    private class NoEmail : IEmailSender
    {
        public Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }
}
