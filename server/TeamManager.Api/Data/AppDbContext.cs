using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<DailyStatus> DailyStatuses => Set<DailyStatus>();
    public DbSet<MonthlyBonus> MonthlyBonuses => Set<MonthlyBonus>();
    public DbSet<MonthLock> MonthLocks => Set<MonthLock>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Employee>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.ToTable(t => t.HasCheckConstraint("CK_Employees_DailyTarget", "[DailyTarget] > 0"));
        });

        b.Entity<Attendance>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.OtherReason).HasMaxLength(200);
            e.ToTable(t => t.HasCheckConstraint(
                "CK_Attendance_OtherReason",
                "[Status] <> 'Other' OR (LEN([OtherReason]) > 0)"));
        });

        b.Entity<DailyStatus>(e =>
        {
            e.ToTable("DailyStatus");
            e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
            e.Property(x => x.Percent).HasPrecision(6, 2);
            e.Property(x => x.Rating).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.WorkPercent).HasDefaultValue(100);
            e.ToTable(t => t.HasCheckConstraint("CK_DailyStatus_WorkPercent", "[WorkPercent] BETWEEN 1 AND 100"));
        });

        b.Entity<ScheduleEntry>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.StartDate, x.EndDate });
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.AbsenceStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Reason).HasMaxLength(200);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ScheduleEntries_Dates", "[EndDate] >= [StartDate]");
                t.HasCheckConstraint("CK_ScheduleEntries_Kind",
                    "([Kind] = 'Absence' AND [AbsenceStatus] IN ('Vacation','Sick','Other') AND [WorkPercent] IS NULL)" +
                    " OR ([Kind] = 'PartialDay' AND [AbsenceStatus] IS NULL AND [WorkPercent] BETWEEN 1 AND 99)");
                t.HasCheckConstraint("CK_ScheduleEntries_OtherReason",
                    "[AbsenceStatus] IS NULL OR [AbsenceStatus] <> 'Other' OR (LEN([Reason]) > 0)");
            });
        });

        b.Entity<MonthlyBonus>(e =>
        {
            e.ToTable("MonthlyBonus");
            e.HasIndex(x => new { x.EmployeeId, x.Year, x.Month }).IsUnique();
            e.Property(x => x.Amount).HasPrecision(8, 2);
            e.Property(x => x.Note).HasMaxLength(500);
            e.ToTable(t => t.HasCheckConstraint("CK_MonthlyBonus_Amount", "[Amount] >= 0"));
        });

        b.Entity<MonthLock>(e =>
        {
            e.ToTable("MonthLock");
            e.HasKey(x => new { x.Year, x.Month });
            e.Property(x => x.LockedBy).HasMaxLength(100);
        });

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(50);
            e.Property(x => x.DisplayName).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<Setting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(1000);
        });

        b.Entity<EmailLog>(e =>
        {
            e.ToTable("EmailLog");
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.To).HasMaxLength(200);
            e.HasIndex(x => new { x.Type, x.ForDate });
        });
    }
}
