using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorTrainer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WordNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WordNotes",
                columns: table => new
                {
                    WordId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordNotes", x => x.WordId);
                    table.ForeignKey(
                        name: "FK_WordNotes_Words_WordId",
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
                name: "WordNotes");
        }
    }
}
