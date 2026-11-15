using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Device",
                table: "Tracks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Device",
                table: "Tracks");
        }
    }
}
