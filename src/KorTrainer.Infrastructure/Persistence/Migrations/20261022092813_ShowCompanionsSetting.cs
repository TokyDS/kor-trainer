using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorTrainer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShowCompanionsSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowCompanions",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                // Companions are on by default, for learners who already have settings too.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowCompanions",
                table: "Settings");
        }
    }
}
