using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingTakes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TakeChannel",
                table: "Mixes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InputCalibrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DeviceIdHint = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LatencySamples = table.Column<int>(type: "INTEGER", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SpreadSamples = table.Column<int>(type: "INTEGER", nullable: true),
                    DetectedClicks = table.Column<int>(type: "INTEGER", nullable: true),
                    OutputLatencySamples = table.Column<int>(type: "INTEGER", nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InputCalibrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecordingTakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Part = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PassIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    Partial = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartSample = table.Column<long>(type: "INTEGER", nullable: false),
                    LengthSamples = table.Column<long>(type: "INTEGER", nullable: false),
                    InputMode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    InputChannels = table.Column<int>(type: "INTEGER", nullable: false),
                    LatencySamples = table.Column<int>(type: "INTEGER", nullable: false),
                    LatencyMethod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CalibrationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeviceLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MixId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Favorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    PeakDbfs = table.Column<double>(type: "REAL", nullable: false),
                    ClippedSamples = table.Column<int>(type: "INTEGER", nullable: false),
                    Dropouts = table.Column<int>(type: "INTEGER", nullable: false),
                    AdjustmentSamples = table.Column<int>(type: "INTEGER", nullable: false),
                    AlignmentRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordingTakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordingTakes_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecordingTakes_InputCalibrations_CalibrationId",
                        column: x => x.CalibrationId,
                        principalTable: "InputCalibrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecordingTakes_Mixes_MixId",
                        column: x => x.MixId,
                        principalTable: "Mixes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RecordingTakes_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InputCalibrations_DeviceLabel",
                table: "InputCalibrations",
                column: "DeviceLabel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordingTakes_AssetId",
                table: "RecordingTakes",
                column: "AssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordingTakes_CalibrationId",
                table: "RecordingTakes",
                column: "CalibrationId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordingTakes_MixId",
                table: "RecordingTakes",
                column: "MixId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordingTakes_TrackId_CreatedAt",
                table: "RecordingTakes",
                columns: new[] { "TrackId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RecordingTakes_TrackId_Number",
                table: "RecordingTakes",
                columns: new[] { "TrackId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordingTakes_TrackId_SessionId",
                table: "RecordingTakes",
                columns: new[] { "TrackId", "SessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecordingTakes");

            migrationBuilder.DropTable(
                name: "InputCalibrations");

            migrationBuilder.DropColumn(
                name: "TakeChannel",
                table: "Mixes");
        }
    }
}
