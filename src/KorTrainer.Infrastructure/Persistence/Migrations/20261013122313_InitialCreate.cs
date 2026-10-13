using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorTrainer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LocalDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Module = table.Column<int>(type: "INTEGER", nullable: false),
                    ExerciseType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ItemKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResponseMs = table.Column<int>(type: "INTEGER", nullable: false),
                    AnsweredUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReadingTrials",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LocalDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SyllableCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ElapsedMs = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadCorrectly = table.Column<bool>(type: "INTEGER", nullable: false),
                    CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingTrials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ShowRomanization = table.Column<bool>(type: "INTEGER", nullable: false),
                    Theme = table.Column<int>(type: "INTEGER", nullable: false),
                    NewWordsPerDay = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxReviewsPerDay = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeechRate = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudySessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LocalDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Module = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessions", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "Words",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeckKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Korean = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Russian = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PartOfSpeech = table.Column<int>(type: "INTEGER", nullable: false),
                    Irregularity = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    LessonDay = table.Column<int>(type: "INTEGER", nullable: true),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Words", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WritingDrafts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LocalDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TaskNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WritingDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReviewLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WordId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Grade = table.Column<int>(type: "INTEGER", nullable: false),
                    PhaseBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    IntervalBefore = table.Column<double>(type: "REAL", nullable: false),
                    IntervalAfter = table.Column<double>(type: "REAL", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewLogs_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WordProgress",
                columns: table => new
                {
                    WordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Phase = table.Column<int>(type: "INTEGER", nullable: false),
                    Ease = table.Column<double>(type: "REAL", nullable: false),
                    IntervalDays = table.Column<double>(type: "REAL", nullable: false),
                    Repetitions = table.Column<int>(type: "INTEGER", nullable: false),
                    Lapses = table.Column<int>(type: "INTEGER", nullable: false),
                    Step = table.Column<int>(type: "INTEGER", nullable: false),
                    DueUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IntroducedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastReviewedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordProgress", x => x.WordId);
                    table.ForeignKey(
                        name: "FK_WordProgress_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseAttempts_LocalDate_Module",
                table: "ExerciseAttempts",
                columns: new[] { "LocalDate", "Module" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingTrials_LocalDate",
                table: "ReadingTrials",
                column: "LocalDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLogs_LocalDate",
                table: "ReviewLogs",
                column: "LocalDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLogs_WordId",
                table: "ReviewLogs",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySessions_LocalDate",
                table: "StudySessions",
                column: "LocalDate");

            migrationBuilder.CreateIndex(
                name: "IX_WordProgress_Phase_DueUtc",
                table: "WordProgress",
                columns: new[] { "Phase", "DueUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Words_DeckKey",
                table: "Words",
                column: "DeckKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Words_Stage_LessonDay_Rank",
                table: "Words",
                columns: new[] { "Stage", "LessonDay", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_Words_Topic",
                table: "Words",
                column: "Topic");

            migrationBuilder.CreateIndex(
                name: "IX_WritingDrafts_LocalDate",
                table: "WritingDrafts",
                column: "LocalDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseAttempts");

            migrationBuilder.DropTable(
                name: "ReadingTrials");

            migrationBuilder.DropTable(
                name: "ReviewLogs");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "StudySessions");

            migrationBuilder.DropTable(
                name: "TaskCompletions");

            migrationBuilder.DropTable(
                name: "WordProgress");

            migrationBuilder.DropTable(
                name: "WritingDrafts");

            migrationBuilder.DropTable(
                name: "Words");
        }
    }
}
