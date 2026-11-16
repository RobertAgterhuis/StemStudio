using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Backend = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OllamaModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OllamaFallbackModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OllamaThink = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClaudeModel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ClaudeEffort = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ClaudeNoticeAcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Limits = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActiveRunId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserMessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Backend = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tainted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Plan = table.Column<string>(type: "TEXT", nullable: false),
                    ModelTurns = table.Column<int>(type: "INTEGER", nullable: false),
                    ToolCalls = table.Column<int>(type: "INTEGER", nullable: false),
                    InvalidCalls = table.Column<int>(type: "INTEGER", nullable: false),
                    InputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    CachedInputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    FailureCode = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    FailureMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRuns_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Seq = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: false),
                    Page = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MixId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ArgumentsHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Approvals_AgentRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunEvents",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Seq = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", maxLength: 16384, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunEvents", x => new { x.RunId, x.Seq });
                    table.ForeignKey(
                        name: "FK_RunEvents_AgentRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Seq = table.Column<int>(type: "INTEGER", nullable: false),
                    CallId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tool = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Risk = table.Column<string>(type: "TEXT", nullable: false),
                    UndoKind = table.Column<string>(type: "TEXT", nullable: false),
                    Arguments = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                    ArgumentsHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PlanIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    Decision = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionReasons = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EstimatedMinutes = table.Column<double>(type: "REAL", nullable: true),
                    ResultSummary = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Progress = table.Column<int>(type: "INTEGER", nullable: true),
                    JobTrackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    JobId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    JobPhase = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunSteps_AgentRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UndoEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ActionTool = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ActionArguments = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                    Fresh = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpectedUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UndoEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UndoEntries_AgentRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AgentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_ConversationId_StartedAt",
                table: "AgentRuns",
                columns: new[] { "ConversationId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentRuns_Status",
                table: "AgentRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_RunId_Status",
                table: "Approvals",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ConversationId_Seq",
                table: "ConversationMessages",
                columns: new[] { "ConversationId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UpdatedAt",
                table: "Conversations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RunSteps_RunId_Seq",
                table: "RunSteps",
                columns: new[] { "RunId", "Seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UndoEntries_ConversationId_Ordinal",
                table: "UndoEntries",
                columns: new[] { "ConversationId", "Ordinal" });

            migrationBuilder.CreateIndex(
                name: "IX_UndoEntries_RunId",
                table: "UndoEntries",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentSettings");

            migrationBuilder.DropTable(
                name: "Approvals");

            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "RunEvents");

            migrationBuilder.DropTable(
                name: "RunSteps");

            migrationBuilder.DropTable(
                name: "UndoEntries");

            migrationBuilder.DropTable(
                name: "AgentRuns");

            migrationBuilder.DropTable(
                name: "Conversations");
        }
    }
}
