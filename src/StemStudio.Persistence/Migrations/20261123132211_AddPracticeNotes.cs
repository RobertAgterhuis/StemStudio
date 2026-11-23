using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <summary>
    /// Feature 020 (data-model 5.2, 5.4): the <c>PracticeNotes</c> table (cascade with the track) and the nullable
    /// <c>Assets.Handout</c> column. Additive only: existing rows keep their data (null = not a handout).
    /// </summary>
    public partial class AddPracticeNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Handout",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PracticeNotes",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Family = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeNotes", x => new { x.TrackId, x.Family });
                    table.ForeignKey(
                        name: "FK_PracticeNotes_Tracks_TrackId",
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
                name: "PracticeNotes");

            // A native column drop (SQLite 3.35+) instead of EF's table rebuild: the rebuild would lose the folder index that
            // AddLibraryFolders created outside the model.
            migrationBuilder.Sql("ALTER TABLE \"Assets\" DROP COLUMN \"Handout\";");
        }
    }
}
