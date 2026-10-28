using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Tests;

public class ScheduleRulesTests
{
    private static readonly DateOnly Day = new(2026, 11, 1);

    private static ScheduleEntryRequest Absence(AttendanceStatus? status, string? reason = null) =>
        new(1, Day, Day, ScheduleKind.Absence, status, reason, null);

    private static ScheduleEntryRequest Partial(int? percent) =>
        new(1, Day, Day, ScheduleKind.PartialDay, null, null, percent);

    [Fact]
    public void Vacation_is_valid()
    {
        Assert.Empty(ScheduleRules.Validate(Absence(AttendanceStatus.Vacation)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(AttendanceStatus.Present)] // "נוכחת" היא לא היעדרות
    public void Absence_needs_an_absence_type(AttendanceStatus? status)
    {
        Assert.Contains("absenceStatus", ScheduleRules.Validate(Absence(status)).Keys);
    }

    [Fact]
    public void Other_absence_needs_a_reason()
    {
        Assert.Contains("reason", ScheduleRules.Validate(Absence(AttendanceStatus.Other, " ")).Keys);
        Assert.Empty(ScheduleRules.Validate(Absence(AttendanceStatus.Other, "השתלמות")));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(99, true)]
    [InlineData(100, false)] // 100% הוא יום רגיל, לא חלקי
    public void Partial_day_percent_is_between_1_and_99(int? percent, bool valid)
    {
        Assert.Equal(valid, ScheduleRules.Validate(Partial(percent)).Count == 0);
    }

    [Fact]
    public void End_before_start_is_rejected()
    {
        var req = new ScheduleEntryRequest(1, Day, Day.AddDays(-1), ScheduleKind.Absence, AttendanceStatus.Vacation, null, null);
        Assert.Contains("endDate", ScheduleRules.Validate(req).Keys);
    }

    [Theory]
    [InlineData(50, 50, 25)]
    [InlineData(45, 50, 23)] // 22.5 מעוגל למעלה
    [InlineData(40, 75, 30)]
    [InlineData(1, 10, 1)]   // לפחות 1
    public void Effective_target_is_rounded_percent_of_base(int baseTarget, int percent, int expected)
    {
        Assert.Equal(expected, ScheduleRules.EffectiveTarget(baseTarget, percent));
    }

    [Fact]
    public void Report_lists_partial_days()
    {
        AttendanceRowDto[] rows =
        [
            new(1, "רחל", true, AttendanceStatus.Present, null, new PlannedDayDto(ScheduleKind.PartialDay, null, null, 50)),
            new(2, "שרה", true, AttendanceStatus.Present, null),
        ];

        var html = AttendanceRules.ReportHtml(Day, rows, changed: null);

        Assert.Contains("עבודה חלקית: רחל (50%)", html);
    }
}

public class ScheduleServiceTests
{
    private static readonly DateOnly Day = new(2026, 10, 27);

    private readonly AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private SettingsService Settings => new(db);
    private ScheduleService Schedule => new(db, Settings);
    private DailyStatusService DailyStatus => new(db, Settings, Schedule);
    private AttendanceService Attendance => new(db, Settings, new NoEmail(), Schedule);

    private Employee AddPresentEmployee(int target)
    {
        var e = new Employee { Name = "רחל", DailyTarget = target };
        db.Employees.Add(e);
        db.SaveChanges();
        db.Attendance.Add(new Attendance { EmployeeId = e.Id, Date = Day, Status = AttendanceStatus.Present });
        db.SaveChanges();
        return e;
    }

    private Task<(ScheduleEntryDto? Entry, ServiceError? Error)> AddPartial(int employeeId, int percent, int? id = null) =>
        Schedule.SaveAsync(id, new(employeeId, Day, Day, ScheduleKind.PartialDay, null, null, percent));

    [Fact]
    public async Task Partial_day_reduces_the_target_for_that_day()
    {
        var e = AddPresentEmployee(50);
        await AddPartial(e.Id, 50);

        await DailyStatus.SaveAsync(Day, [new(e.Id, 25)]);

        var row = Assert.Single((await DailyStatus.GetDayAsync(Day)).Rows);
        Assert.Equal(25, row.Target);
        Assert.Equal(50, row.WorkPercent);
        Assert.Equal(100.00m, row.Percent);
        Assert.Equal(Rating.Excellent, row.Rating);
    }

    [Fact]
    public async Task Adding_partial_day_after_output_recalculates_status()
    {
        var e = AddPresentEmployee(50);
        await DailyStatus.SaveAsync(Day, [new(e.Id, 25)]); // 50% = חלש

        await AddPartial(e.Id, 50);

        var row = await db.DailyStatuses.SingleAsync();
        Assert.Equal(25, row.TargetSnapshot);
        Assert.Equal(50, row.BaseTarget);
        Assert.Equal(Rating.Excellent, row.Rating);
    }

    [Fact]
    public async Task Removing_partial_day_restores_full_target()
    {
        var e = AddPresentEmployee(50);
        var (entry, _) = await AddPartial(e.Id, 50);
        await DailyStatus.SaveAsync(Day, [new(e.Id, 25)]);

        await Schedule.DeleteAsync(entry!.Id);

        var row = await db.DailyStatuses.SingleAsync();
        Assert.Equal(50, row.TargetSnapshot);
        Assert.Equal(Rating.Weak, row.Rating);
    }

    [Fact]
    public async Task Overlapping_entries_for_same_employee_are_rejected()
    {
        var e = AddPresentEmployee(50);
        await Schedule.SaveAsync(null, new(e.Id, Day.AddDays(-2), Day.AddDays(2), ScheduleKind.Absence, AttendanceStatus.Vacation, null, null));

        var (_, error) = await AddPartial(e.Id, 50);

        Assert.NotNull(error);
    }

    [Fact]
    public async Task Entries_in_a_locked_month_are_rejected()
    {
        var e = AddPresentEmployee(50);
        db.MonthLocks.Add(new MonthLock { Year = 2026, Month = 10, LockedBy = "teamlead" });
        await db.SaveChangesAsync();

        var (_, error) = await AddPartial(e.Id, 50);

        Assert.Equal(409, error?.StatusCode);
    }

    [Fact]
    public async Task Attendance_shows_what_is_planned()
    {
        var e = new Employee { Name = "שרה", DailyTarget = 40 };
        db.Employees.Add(e);
        await db.SaveChangesAsync();
        await Schedule.SaveAsync(null, new(e.Id, Day, Day.AddDays(4), ScheduleKind.Absence, AttendanceStatus.Vacation, "ignored", null));

        var row = Assert.Single((await Attendance.GetDayAsync(Day)).Rows);

        Assert.Null(row.Status); // לא נשמר אוטומטית; הלקוח ממלא את זה כברירת מחדל
        Assert.Equal(ScheduleKind.Absence, row.Planned?.Kind);
        Assert.Equal(AttendanceStatus.Vacation, row.Planned?.AbsenceStatus);
        Assert.Null(row.Planned?.Reason); // סיבה נשמרת רק ב"אחר"
    }

    private class NoEmail : IEmailSender
    {
        public Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }
}
