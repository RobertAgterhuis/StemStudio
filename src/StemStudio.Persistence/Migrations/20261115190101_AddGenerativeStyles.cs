using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGenerativeStyles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Generations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Restyle = table.Column<string>(type: "TEXT", nullable: true),
                    SourceTrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GeneratorJobId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Device = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Seeds = table.Column<string>(type: "TEXT", nullable: false),
                    Takes = table.Column<string>(type: "TEXT", nullable: false),
                    Warnings = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Generations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Styles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PresetKey = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    GenreFamily = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SubGenre = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    TempoFeel = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    VocalHandling = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DefaultStrength = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Instrumentation = table.Column<string>(type: "TEXT", nullable: false),
                    Moods = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Styles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Generations_CreatedAt",
                table: "Generations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Generations_SourceTrackId",
                table: "Generations",
                column: "SourceTrackId");

            migrationBuilder.CreateIndex(
                name: "IX_Generations_Status",
                table: "Generations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Styles_NormalizedName",
                table: "Styles",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Generations");

            migrationBuilder.DropTable(
                name: "Styles");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Tracks");
        }
    }
}
