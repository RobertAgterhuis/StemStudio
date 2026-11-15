using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackLyrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LyricsStatus",
                table: "Tracks",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "None"); // tracks from before feature 006 have no lyrics yet

            migrationBuilder.CreateTable(
                name: "TrackLyrics",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    LyricsJobId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Document = table.Column<string>(type: "TEXT", nullable: true),
                    OffsetMs = table.Column<int>(type: "INTEGER", nullable: false),
                    AlignEstimateSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    AlignDevice = table.Column<string>(type: "TEXT", nullable: true),
                    AlignProgress = table.Column<int>(type: "INTEGER", nullable: true),
                    AutoAlign = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackLyrics", x => x.TrackId);
                    table.ForeignKey(
                        name: "FK_TrackLyrics_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackLyrics");

            migrationBuilder.DropColumn(
                name: "LyricsStatus",
                table: "Tracks");
        }
    }
}
