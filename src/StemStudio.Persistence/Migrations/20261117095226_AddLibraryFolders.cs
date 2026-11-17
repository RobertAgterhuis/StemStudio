using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StemStudio.Persistence.Migrations
{
    /// <summary>
    /// Feature 022 (research R-13): placement, item library info, <c>FiledVersion</c> (0 = not filed; the start-up backfill
    /// files every item) and <c>FiledAt</c> on <c>Assets</c>, track library info on <c>Tracks</c>, the folder index
    /// <c>IX_Assets_Folder</c> (created here, outside the model, because its columns belong to the complex property) and <c>IX_Assets_FiledVersion</c>.
    /// Only columns and indexes are added (013 restore accepts it). <b>Down</b> drops them: the user's library info is lost.
    /// </summary>
    public partial class AddLibraryFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LibraryAlbum",
                table: "Tracks",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LibraryArtist",
                table: "Tracks",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LibrarySong",
                table: "Tracks",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FiledAt",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FiledVersion",
                table: "Assets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FolderAlbum",
                table: "Assets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderAlbumKey",
                table: "Assets",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderAlbumSource",
                table: "Assets",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderArtist",
                table: "Assets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderArtistKey",
                table: "Assets",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderArtistSource",
                table: "Assets",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderSong",
                table: "Assets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderSongKey",
                table: "Assets",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderSongSource",
                table: "Assets",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FolderYear",
                table: "Assets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LibraryAlbum",
                table: "Assets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LibraryArtist",
                table: "Assets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LibrarySong",
                table: "Assets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Placement_Discriminator",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_FiledVersion",
                table: "Assets",
                column: "FiledVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Folder",
                table: "Assets",
                columns: ["FolderArtistKey", "FolderAlbumKey", "FolderSongKey"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_Folder",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_FiledVersion",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LibraryAlbum",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "LibraryArtist",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "LibrarySong",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "FiledAt",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FiledVersion",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderAlbum",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderAlbumKey",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderAlbumSource",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderArtist",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderArtistKey",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderArtistSource",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderSong",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderSongKey",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderSongSource",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "FolderYear",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LibraryAlbum",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LibraryArtist",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LibrarySong",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Placement_Discriminator",
                table: "Assets");
        }
    }
}
