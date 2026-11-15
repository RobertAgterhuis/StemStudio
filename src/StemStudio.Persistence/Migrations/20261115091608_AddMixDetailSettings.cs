using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMixDetailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetailChannels",
                table: "Mixes",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Expanded",
                table: "Mixes",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetailChannels",
                table: "Mixes");

            migrationBuilder.DropColumn(
                name: "Expanded",
                table: "Mixes");
        }
    }
}
