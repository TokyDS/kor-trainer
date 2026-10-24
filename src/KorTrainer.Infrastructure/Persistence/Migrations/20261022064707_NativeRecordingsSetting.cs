using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KorTrainer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NativeRecordingsSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NativeRecordings",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NativeRecordings",
                table: "Settings");
        }
    }
}
