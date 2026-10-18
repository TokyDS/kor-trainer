using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorTrainer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WordHanja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Hanja",
                table: "Words",
                type: "TEXT",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hanja",
                table: "Words");
        }
    }
}
