CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "Tracks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Tracks" PRIMARY KEY,
    "SourceType" TEXT NOT NULL,
    "SourceKey" TEXT NOT NULL,
    "SourceUrl" TEXT NULL,
    "Title" TEXT NOT NULL,
    "Artist" TEXT NULL,
    "Status" TEXT NOT NULL,
    "Stage" TEXT NULL,
    "Progress" INTEGER NOT NULL,
    "Error" TEXT NULL,
    "WorkerJobId" TEXT NULL,
    "UploadFileName" TEXT NULL,
    "DurationSeconds" REAL NULL,
    "Model" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "BeatGrid" TEXT NULL,
    "Stems" TEXT NOT NULL
);

CREATE TABLE "Mixes" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Mixes" PRIMARY KEY,
    "TrackId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "NormalizedName" TEXT NOT NULL,
    "MasterVolume" REAL NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "Channels" TEXT NOT NULL,
    "Loop" TEXT NULL,
    "Metronome" TEXT NOT NULL,
    CONSTRAINT "FK_Mixes_Tracks_TrackId" FOREIGN KEY ("TrackId") REFERENCES "Tracks" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_Mixes_TrackId_NormalizedName" ON "Mixes" ("TrackId", "NormalizedName");

CREATE INDEX "IX_Mixes_UpdatedAt" ON "Mixes" ("UpdatedAt");

CREATE UNIQUE INDEX "IX_Tracks_SourceKey" ON "Tracks" ("SourceKey");

CREATE INDEX "IX_Tracks_UpdatedAt" ON "Tracks" ("UpdatedAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261114215330_Initial', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Tracks" ADD "DetailPhase" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115091546_AddDetailPhase', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Mixes" ADD "DetailChannels" TEXT NOT NULL DEFAULT '[]';

ALTER TABLE "Mixes" ADD "Expanded" TEXT NOT NULL DEFAULT '[]';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115091608_AddMixDetailSettings', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Tracks" ADD "Device" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115115115_AddJobDevice', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Tracks" ADD "ArtistId" TEXT NULL;

ALTER TABLE "Tracks" ADD "Artwork" TEXT NULL;

ALTER TABLE "Tracks" ADD "AutoArtwork" TEXT NULL;

ALTER TABLE "Tracks" ADD "Metadata" TEXT NOT NULL DEFAULT '{}';

CREATE TABLE "Artists" (
    "Mbid" TEXT NOT NULL CONSTRAINT "PK_Artists" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Type" TEXT NULL,
    "Country" TEXT NULL,
    "BeginYear" INTEGER NULL,
    "EndYear" INTEGER NULL,
    "Website" TEXT NULL,
    "WikidataId" TEXT NULL,
    "FetchedAt" TEXT NOT NULL,
    "Genres" TEXT NOT NULL,
    "Members" TEXT NOT NULL,
    "Summary" TEXT NULL
);

CREATE INDEX "IX_Tracks_ArtistId" ON "Tracks" ("ArtistId");

CREATE TABLE "ef_temp_Tracks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Tracks" PRIMARY KEY,
    "Artist" TEXT NULL,
    "ArtistId" TEXT NULL,
    "Artwork" TEXT NULL,
    "AutoArtwork" TEXT NULL,
    "BeatGrid" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "DetailPhase" TEXT NULL,
    "Device" TEXT NULL,
    "DurationSeconds" REAL NULL,
    "Error" TEXT NULL,
    "Metadata" TEXT NOT NULL,
    "Model" TEXT NULL,
    "Progress" INTEGER NOT NULL,
    "SourceKey" TEXT NOT NULL,
    "SourceType" TEXT NOT NULL,
    "SourceUrl" TEXT NULL,
    "Stage" TEXT NULL,
    "Status" TEXT NOT NULL,
    "Stems" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "UploadFileName" TEXT NULL,
    "WorkerJobId" TEXT NULL,
    CONSTRAINT "FK_Tracks_Artists_ArtistId" FOREIGN KEY ("ArtistId") REFERENCES "Artists" ("Mbid") ON DELETE SET NULL
);

INSERT INTO "ef_temp_Tracks" ("Id", "Artist", "ArtistId", "Artwork", "AutoArtwork", "BeatGrid", "CreatedAt", "DetailPhase", "Device", "DurationSeconds", "Error", "Metadata", "Model", "Progress", "SourceKey", "SourceType", "SourceUrl", "Stage", "Status", "Stems", "Title", "UpdatedAt", "UploadFileName", "WorkerJobId")
SELECT "Id", "Artist", "ArtistId", "Artwork", "AutoArtwork", "BeatGrid", "CreatedAt", "DetailPhase", "Device", "DurationSeconds", "Error", "Metadata", "Model", "Progress", "SourceKey", "SourceType", "SourceUrl", "Stage", "Status", "Stems", "Title", "UpdatedAt", "UploadFileName", "WorkerJobId"
FROM "Tracks";

COMMIT;

PRAGMA foreign_keys = 0;

BEGIN TRANSACTION;
DROP TABLE "Tracks";

ALTER TABLE "ef_temp_Tracks" RENAME TO "Tracks";

COMMIT;

PRAGMA foreign_keys = 1;

BEGIN TRANSACTION;
CREATE INDEX "IX_Tracks_ArtistId" ON "Tracks" ("ArtistId");

CREATE UNIQUE INDEX "IX_Tracks_SourceKey" ON "Tracks" ("SourceKey");

CREATE INDEX "IX_Tracks_UpdatedAt" ON "Tracks" ("UpdatedAt");

COMMIT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115143937_AddArtworkAndArtists', '10.0.15');

BEGIN TRANSACTION;
CREATE TABLE "Assets" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Assets" PRIMARY KEY,
    "Kind" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Progress" INTEGER NOT NULL,
    "Error" TEXT NULL,
    "WorkerJobId" TEXT NULL,
    "Audio" TEXT NULL,
    "Provenance" TEXT NOT NULL,
    "TrackId" TEXT NULL,
    "SizeBytes" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "Files" TEXT NOT NULL
);

CREATE INDEX "IX_Assets_CreatedAt" ON "Assets" ("CreatedAt");

CREATE INDEX "IX_Assets_Status" ON "Assets" ("Status");

CREATE INDEX "IX_Assets_TrackId" ON "Assets" ("TrackId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115152313_AddAssets', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Tracks" ADD "LyricsStatus" TEXT NOT NULL DEFAULT 'None';

CREATE TABLE "TrackLyrics" (
    "TrackId" TEXT NOT NULL CONSTRAINT "PK_TrackLyrics" PRIMARY KEY,
    "Status" TEXT NOT NULL,
    "Reason" TEXT NULL,
    "LyricsJobId" TEXT NULL,
    "Document" TEXT NULL,
    "OffsetMs" INTEGER NOT NULL,
    "AlignEstimateSeconds" INTEGER NULL,
    "AlignDevice" TEXT NULL,
    "AlignProgress" INTEGER NULL,
    "AutoAlign" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_TrackLyrics_Tracks_TrackId" FOREIGN KEY ("TrackId") REFERENCES "Tracks" ("Id") ON DELETE CASCADE
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115163349_AddTrackLyrics', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Assets" ADD "Score" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115171827_AddScoreFacts', '10.0.15');

COMMIT;

