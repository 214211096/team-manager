using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceEmailForDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ForDate",
                table: "EmailLog",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailLog_Type_ForDate",
                table: "EmailLog",
                columns: new[] { "Type", "ForDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailLog_Type_ForDate",
                table: "EmailLog");

            migrationBuilder.DropColumn(
                name: "ForDate",
                table: "EmailLog");
        }
    }
}
