using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <summary>
    /// Feature 020 (data-model 5.1): the nullable <c>Tracks.QualityReport</c> JSON column. Additive only: existing tracks keep
    /// their data (null = never made).
    /// </summary>
    public partial class AddQualityReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QualityReport",
                table: "Tracks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QualityReport",
                table: "Tracks");
        }
    }
}
