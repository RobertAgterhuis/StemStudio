using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Artist = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    WorkerJobId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    UploadFileName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BeatGrid = table.Column<string>(type: "TEXT", nullable: true),
                    Stems = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mixes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MasterVolume = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Channels = table.Column<string>(type: "TEXT", nullable: false),
                    Loop = table.Column<string>(type: "TEXT", nullable: true),
                    Metronome = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mixes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mixes_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mixes_TrackId_NormalizedName",
                table: "Mixes",
                columns: new[] { "TrackId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mixes_UpdatedAt",
                table: "Mixes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_SourceKey",
                table: "Tracks",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_UpdatedAt",
                table: "Tracks",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mixes");

            migrationBuilder.DropTable(
                name: "Tracks");
        }
    }
}
