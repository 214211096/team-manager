using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Tests;

public class DailyStatusServiceTests
{
    private static readonly DateOnly Day = new(2026, 10, 27);

    private readonly AppDbContext db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private SettingsService Settings => new(db);
    private ScheduleService Schedule => new(db, Settings);
    private DailyStatusService Service => new(db, Settings, Schedule);

    private Employee AddEmployee(string name, int target, AttendanceStatus? status)
    {
        var e = new Employee { Name = name, DailyTarget = target };
        db.Employees.Add(e);
        db.SaveChanges();
        if (status is { } s)
        {
            db.Attendance.Add(new Attendance { EmployeeId = e.Id, Date = Day, Status = s, OtherReason = s == AttendanceStatus.Other ? "x" : null });
            db.SaveChanges();
        }
        return e;
    }

    [Fact]
    public async Task Only_present_employees_are_listed()
    {
        AddEmployee("נוכחת", 50, AttendanceStatus.Present);
        AddEmployee("חולה", 50, AttendanceStatus.Sick);
        AddEmployee("לא דווחה", 50, null);

        var day = await Service.GetDayAsync(Day);

        Assert.Equal(["נוכחת"], day.Rows.Select(r => r.EmployeeName));
        Assert.Equal(2, day.AttendanceCount);
    }

    [Fact]
    public async Task Save_calculates_percent_and_rating()
    {
        var e = AddEmployee("רחל", 50, AttendanceStatus.Present);

        Assert.Null(await Service.SaveAsync(Day, [new(e.Id, 45)]));

        var row = Assert.Single((await Service.GetDayAsync(Day)).Rows);
        Assert.Equal(90.00m, row.Percent);
        Assert.Equal(Rating.Good, row.Rating); // 90% בדיוק = טוב
    }

    [Fact]
    public async Task Thresholds_come_from_settings()
    {
        await new SettingsService(db).SaveAsync(new AppSettingsDto
        {
            ReminderTime = "09:30", WeakThreshold = 50, ExcellentThreshold = 60, BonusCap = 600,
        });
        var e = AddEmployee("רחל", 100, AttendanceStatus.Present);

        await Service.SaveAsync(Day, [new(e.Id, 70)]);

        Assert.Equal(Rating.Excellent, Assert.Single((await Service.GetDayAsync(Day)).Rows).Rating);
    }

    [Fact]
    public async Task Target_of_the_day_is_kept_when_employee_target_changes()
    {
        var e = AddEmployee("רחל", 50, AttendanceStatus.Present);
        await Service.SaveAsync(Day, [new(e.Id, 40)]);

        e.DailyTarget = 100;
        await db.SaveChangesAsync();
        await Service.SaveAsync(Day, [new(e.Id, 45)]);

        var row = Assert.Single((await Service.GetDayAsync(Day)).Rows);
        Assert.Equal(50, row.Target);
        Assert.Equal(90.00m, row.Percent);
    }

    [Fact]
    public async Task Cannot_enter_output_for_absent_employee()
    {
        var e = AddEmployee("חולה", 50, AttendanceStatus.Sick);

        var error = await Service.SaveAsync(Day, [new(e.Id, 40)]);

        Assert.NotNull(error);
        Assert.Empty(db.DailyStatuses);
    }

    [Fact]
    public async Task Empty_output_removes_the_day()
    {
        var e = AddEmployee("רחל", 50, AttendanceStatus.Present);
        await Service.SaveAsync(Day, [new(e.Id, 40)]);

        await Service.SaveAsync(Day, [new(e.Id, null)]);

        Assert.Empty(db.DailyStatuses);
    }

    [Fact]
    public async Task Marking_employee_absent_removes_her_output()
    {
        var e = AddEmployee("רחל", 50, AttendanceStatus.Present);
        await Service.SaveAsync(Day, [new(e.Id, 40)]);

        var attendance = new AttendanceService(db, Settings, new NoEmail(), Schedule);
        await attendance.SaveAsync(Day, [new(e.Id, AttendanceStatus.Sick, null)]);

        Assert.Empty(db.DailyStatuses);
    }

    private class NoEmail : IEmailSender
    {
        public Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }
}
