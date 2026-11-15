using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArtworkAndArtists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ArtistId",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Artwork",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoArtwork",
                table: "Tracks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Tracks",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "Artists",
                columns: table => new
                {
                    Mbid = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    BeginYear = table.Column<int>(type: "INTEGER", nullable: true),
                    EndYear = table.Column<int>(type: "INTEGER", nullable: true),
                    Website = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    WikidataId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Genres = table.Column<string>(type: "TEXT", nullable: false),
                    Members = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.Mbid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_ArtistId",
                table: "Tracks",
                column: "ArtistId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Artists_ArtistId",
                table: "Tracks",
                column: "ArtistId",
                principalTable: "Artists",
                principalColumn: "Mbid",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Artists_ArtistId",
                table: "Tracks");

            migrationBuilder.DropTable(
                name: "Artists");

            migrationBuilder.DropIndex(
                name: "IX_Tracks_ArtistId",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "ArtistId",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Artwork",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "AutoArtwork",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Tracks");
        }
    }
}
