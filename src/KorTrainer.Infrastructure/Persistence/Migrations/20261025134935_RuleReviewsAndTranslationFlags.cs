using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorTrainer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RuleReviewsAndTranslationFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RuleReviews",
                columns: table => new
                {
                    ExerciseType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Box = table.Column<int>(type: "INTEGER", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LastCheckedDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Lapses = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleReviews", x => x.ExerciseType);
                });

            migrationBuilder.CreateTable(
                name: "TranslationFlags",
                columns: table => new
                {
                    WordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Russian = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationFlags", x => x.WordId);
                    table.ForeignKey(
                        name: "FK_TranslationFlags_Words_WordId",
                        column: x => x.WordId,
                        principalTable: "Words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RuleReviews");

            migrationBuilder.DropTable(
                name: "TranslationFlags");
        }
    }
}
