using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorTrainer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SelfPacedPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RemindersEnabled",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "PlanSteps",
                columns: table => new
                {
                    StepKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanSteps", x => x.StepKey);
                });

            migrationBuilder.CreateTable(
                name: "StepTaskCompletions",
                columns: table => new
                {
                    StepKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    TaskKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepTaskCompletions", x => new { x.StepKey, x.TaskKey });
                });

            // Manual ticks of the calendar plan belonged to a day; days 1–10 were lessons 1–10.
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO StepTaskCompletions (StepKey, TaskKey, CompletedUtc)
                SELECT 'lesson-' || day, TaskKey, CompletedUtc
                FROM (
                    SELECT CAST(julianday(t.Date) - julianday(s.StartDate) AS INTEGER) + 1 AS day, t.TaskKey, t.CompletedUtc
                    FROM TaskCompletions t, Settings s
                )
                WHERE day BETWEEN 1 AND 10;
                """);

            migrationBuilder.DropTable(
                name: "TaskCompletions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanSteps");

            migrationBuilder.DropTable(
                name: "StepTaskCompletions");

            migrationBuilder.DropColumn(
                name: "RemindersEnabled",
                table: "Settings");

            migrationBuilder.CreateTable(
                name: "TaskCompletions",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TaskKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskCompletions", x => new { x.Date, x.TaskKey });
                });
        }
    }
}
