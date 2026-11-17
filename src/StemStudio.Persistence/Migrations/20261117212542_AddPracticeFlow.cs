using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticeFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PracticeLogSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NoticeAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeLogSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PracticeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MixId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlayedSeconds = table.Column<double>(type: "REAL", nullable: false),
                    LoopPasses = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxSpeedPercent = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PracticeSessions_Mixes_MixId",
                        column: x => x.MixId,
                        principalTable: "Mixes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PracticeSessions_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScoreViews",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OffsetSeconds = table.Column<double>(type: "REAL", nullable: false),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    BarsJson = table.Column<string>(type: "TEXT", nullable: false),
                    WorkerJobId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreViews", x => x.AssetId);
                    table.ForeignKey(
                        name: "FK_ScoreViews_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Setlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RemovedEntries = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Setlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackSections",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Analyzer = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Snap = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: false),
                    WorkerJobId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackSections", x => x.TrackId);
                    table.ForeignKey(
                        name: "FK_TrackSections_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeSectionTimes",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PlayedSeconds = table.Column<double>(type: "REAL", nullable: false),
                    LoopPasses = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainerReachedPercent = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSectionTimes", x => new { x.SessionId, x.Index });
                    table.ForeignKey(
                        name: "FK_PracticeSectionTimes_PracticeSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PracticeSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SetlistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SetlistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MixId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetlistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SetlistEntries_Mixes_MixId",
                        column: x => x.MixId,
                        principalTable: "Mixes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SetlistEntries_Setlists_SetlistId",
                        column: x => x.SetlistId,
                        principalTable: "Setlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SetlistEntries_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    StartSeconds = table.Column<double>(type: "REAL", nullable: false),
                    EndSeconds = table.Column<double>(type: "REAL", nullable: false),
                    Instrument = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: true),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_TrackSections_TrackId",
                        column: x => x.TrackId,
                        principalTable: "TrackSections",
                        principalColumn: "TrackId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PracticeLogSettings",
                columns: new[] { "Id", "Enabled", "NoticeAcknowledged", "UpdatedAt" },
                values: new object[] { 1, true, false, null });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_MixId",
                table: "PracticeSessions",
                column: "MixId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_StartedAt",
                table: "PracticeSessions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_TrackId_StartedAt",
                table: "PracticeSessions",
                columns: new[] { "TrackId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreViews_Status",
                table: "ScoreViews",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreViews_TrackId",
                table: "ScoreViews",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_TrackId_Position",
                table: "Sections",
                columns: new[] { "TrackId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_SetlistEntries_MixId",
                table: "SetlistEntries",
                column: "MixId");

            migrationBuilder.CreateIndex(
                name: "IX_SetlistEntries_SetlistId_Position",
                table: "SetlistEntries",
                columns: new[] { "SetlistId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_SetlistEntries_TrackId",
                table: "SetlistEntries",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_Setlists_UpdatedAt",
                table: "Setlists",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TrackSections_Status",
                table: "TrackSections",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PracticeLogSettings");

            migrationBuilder.DropTable(
                name: "PracticeSectionTimes");

            migrationBuilder.DropTable(
                name: "ScoreViews");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "SetlistEntries");

            migrationBuilder.DropTable(
                name: "PracticeSessions");

            migrationBuilder.DropTable(
                name: "TrackSections");

            migrationBuilder.DropTable(
                name: "Setlists");
        }
    }
}
