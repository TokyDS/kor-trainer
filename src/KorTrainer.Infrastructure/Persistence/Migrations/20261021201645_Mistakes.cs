using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorTrainer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Mistakes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mistakes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Module = table.Column<int>(type: "INTEGER", nullable: false),
                    ExerciseType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ItemKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    Box = table.Column<int>(type: "INTEGER", nullable: false),
                    Lapses = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ResolvedDate = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mistakes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mistakes_Module_ItemKey",
                table: "Mistakes",
                columns: new[] { "Module", "ItemKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mistakes_ResolvedDate_DueDate",
                table: "Mistakes",
                columns: new[] { "ResolvedDate", "DueDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mistakes");
        }
    }
}
