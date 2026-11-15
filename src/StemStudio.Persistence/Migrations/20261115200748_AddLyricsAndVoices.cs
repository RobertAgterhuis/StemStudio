using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLyricsAndVoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Song",
                table: "Generations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoiceConversion",
                table: "Generations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoiceProfileId",
                table: "Generations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AudioHashes",
                columns: table => new
                {
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioHashes", x => x.Sha256);
                });

            migrationBuilder.CreateTable(
                name: "LyricsSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LyricsSheets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VoiceProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Consent = table.Column<string>(type: "TEXT", nullable: false),
                    SampleSource = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SamplePath = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SamplePcmSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SampleFileSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    VoicedRatio = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoiceProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Generations_VoiceProfileId",
                table: "Generations",
                column: "VoiceProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioHashes_OwnerId",
                table: "AudioHashes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_LyricsSheets_UpdatedAt",
                table: "LyricsSheets",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VoiceProfiles_NormalizedName",
                table: "VoiceProfiles",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioHashes");

            migrationBuilder.DropTable(
                name: "LyricsSheets");

            migrationBuilder.DropTable(
                name: "VoiceProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Generations_VoiceProfileId",
                table: "Generations");

            migrationBuilder.DropColumn(
                name: "Song",
                table: "Generations");

            migrationBuilder.DropColumn(
                name: "VoiceConversion",
                table: "Generations");

            migrationBuilder.DropColumn(
                name: "VoiceProfileId",
                table: "Generations");
        }
    }
}
