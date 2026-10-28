using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BaseTarget",
                table: "DailyStatus",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkPercent",
                table: "DailyStatus",
                type: "int",
                nullable: false,
                defaultValue: 100);

            // רשומות קיימות נוצרו לפני ימים חלקיים: היעד המלא זהה ליעד בפועל.
            migrationBuilder.Sql("UPDATE [DailyStatus] SET [BaseTarget] = [TargetSnapshot]");

            migrationBuilder.CreateTable(
                name: "ScheduleEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AbsenceStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkPercent = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleEntries", x => x.Id);
                    table.CheckConstraint("CK_ScheduleEntries_Dates", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_ScheduleEntries_Kind", "([Kind] = 'Absence' AND [AbsenceStatus] IN ('Vacation','Sick','Other') AND [WorkPercent] IS NULL) OR ([Kind] = 'PartialDay' AND [AbsenceStatus] IS NULL AND [WorkPercent] BETWEEN 1 AND 99)");
                    table.CheckConstraint("CK_ScheduleEntries_OtherReason", "[AbsenceStatus] IS NULL OR [AbsenceStatus] <> 'Other' OR (LEN([Reason]) > 0)");
                    table.ForeignKey(
                        name: "FK_ScheduleEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DailyStatus_WorkPercent",
                table: "DailyStatus",
                sql: "[WorkPercent] BETWEEN 1 AND 100");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleEntries_EmployeeId_StartDate_EndDate",
                table: "ScheduleEntries",
                columns: new[] { "EmployeeId", "StartDate", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduleEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DailyStatus_WorkPercent",
                table: "DailyStatus");

            migrationBuilder.DropColumn(
                name: "BaseTarget",
                table: "DailyStatus");

            migrationBuilder.DropColumn(
                name: "WorkPercent",
                table: "DailyStatus");
        }
    }
}
