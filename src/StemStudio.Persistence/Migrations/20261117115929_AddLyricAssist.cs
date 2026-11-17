using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLyricAssist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastCause",
                table: "LyricsSheets",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Created");

            migrationBuilder.AddColumn<string>(
                name: "OwnWorkOverride",
                table: "LyricsSheets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "LyricsSheets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "RhythmTarget",
                table: "LyricsSheets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sensitivity",
                table: "LyricsSheets",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Balanced");

            migrationBuilder.CreateTable(
                name: "LyricAssists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SheetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SheetRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Categories = table.Column<string>(type: "TEXT", nullable: false),
                    LineFrom = table.Column<int>(type: "INTEGER", nullable: true),
                    LineTo = table.Column<int>(type: "INTEGER", nullable: true),
                    Count = table.Column<int>(type: "INTEGER", nullable: true),
                    Instruction = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Sensitivity = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Layers = table.Column<string>(type: "TEXT", nullable: false),
                    Backend = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UsingFallback = table.Column<bool>(type: "INTEGER", nullable: false),
                    BlocksTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    BlocksDone = table.Column<int>(type: "INTEGER", nullable: false),
                    BlocksSkipped = table.Column<int>(type: "INTEGER", nullable: false),
                    Counts = table.Column<string>(type: "TEXT", nullable: false),
                    Discarded = table.Column<string>(type: "TEXT", nullable: false),
                    Usage = table.Column<string>(type: "TEXT", nullable: true),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    AgentRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FailureCode = table.Column<string>(type: "TEXT", maxLength: 24, nullable: true),
                    FailureMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FailureFix = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EstimatedSeconds = table.Column<double>(type: "REAL", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LyricAssists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LyricAssists_LyricsSheets_SheetId",
                        column: x => x.SheetId,
                        principalTable: "LyricsSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LyricAssistSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaudeNoticeAcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClaudeNoticeVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LyricAssistSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LyricsAssistedLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LineText = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    SuggestionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SheetId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LyricsAssistedLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LyricsAssistedLines_LyricsSheets_SheetId",
                        column: x => x.SheetId,
                        principalTable: "LyricsSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LyricsIntentionalMarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SheetId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LyricsIntentionalMarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LyricsIntentionalMarks_LyricsSheets_SheetId",
                        column: x => x.SheetId,
                        principalTable: "LyricsSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LyricsSheetRevisions",
                columns: table => new
                {
                    SheetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Cause = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    AcceptGroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LyricsSheetRevisions", x => new { x.SheetId, x.Revision });
                    table.ForeignKey(
                        name: "FK_LyricsSheetRevisions_LyricsSheets_SheetId",
                        column: x => x.SheetId,
                        principalTable: "LyricsSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LyricWords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Word = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LyricWords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LyricAssistEvents",
                columns: table => new
                {
                    AssistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Seq = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Data = table.Column<string>(type: "TEXT", maxLength: 16384, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LyricAssistEvents", x => new { x.AssistId, x.Seq });
                    table.ForeignKey(
                        name: "FK_LyricAssistEvents_LyricAssists_AssistId",
                        column: x => x.AssistId,
                        principalTable: "LyricAssists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LyricSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssistId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SheetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Anchor = table.Column<string>(type: "TEXT", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Proposal = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Explanation = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SyllablesBefore = table.Column<int>(type: "INTEGER", nullable: true),
                    SyllablesAfter = table.Column<int>(type: "INTEGER", nullable: true),
                    RhymeWith = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AcceptGroupId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LyricSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LyricSuggestions_LyricAssists_AssistId",
                        column: x => x.AssistId,
                        principalTable: "LyricAssists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LyricSuggestions_LyricsSheets_SheetId",
                        column: x => x.SheetId,
                        principalTable: "LyricsSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LyricAssists_SheetId_CreatedAt",
                table: "LyricAssists",
                columns: new[] { "SheetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LyricAssists_Status",
                table: "LyricAssists",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LyricsAssistedLines_SheetId",
                table: "LyricsAssistedLines",
                column: "SheetId");

            migrationBuilder.CreateIndex(
                name: "IX_LyricsIntentionalMarks_SheetId",
                table: "LyricsIntentionalMarks",
                column: "SheetId");

            migrationBuilder.CreateIndex(
                name: "IX_LyricSuggestions_AssistId",
                table: "LyricSuggestions",
                column: "AssistId");

            migrationBuilder.CreateIndex(
                name: "IX_LyricSuggestions_SheetId_Status",
                table: "LyricSuggestions",
                columns: new[] { "SheetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LyricWords_Language_Word",
                table: "LyricWords",
                columns: new[] { "Language", "Word" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LyricAssistEvents");

            migrationBuilder.DropTable(
                name: "LyricAssistSettings");

            migrationBuilder.DropTable(
                name: "LyricsAssistedLines");

            migrationBuilder.DropTable(
                name: "LyricsIntentionalMarks");

            migrationBuilder.DropTable(
                name: "LyricsSheetRevisions");

            migrationBuilder.DropTable(
                name: "LyricSuggestions");

            migrationBuilder.DropTable(
                name: "LyricWords");

            migrationBuilder.DropTable(
                name: "LyricAssists");

            migrationBuilder.DropColumn(
                name: "LastCause",
                table: "LyricsSheets");

            migrationBuilder.DropColumn(
                name: "OwnWorkOverride",
                table: "LyricsSheets");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "LyricsSheets");

            migrationBuilder.DropColumn(
                name: "RhythmTarget",
                table: "LyricsSheets");

            migrationBuilder.DropColumn(
                name: "Sensitivity",
                table: "LyricsSheets");
        }
    }
}
