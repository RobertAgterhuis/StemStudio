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

BEGIN TRANSACTION;
ALTER TABLE "Tracks" ADD "Origin" TEXT NULL;

CREATE TABLE "Generations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Generations" PRIMARY KEY,
    "Kind" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Progress" INTEGER NOT NULL,
    "Stage" TEXT NULL,
    "Restyle" TEXT NULL,
    "SourceTrackId" TEXT NULL,
    "GeneratorJobId" TEXT NULL,
    "Device" TEXT NULL,
    "Error" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "CompletedAt" TEXT NULL,
    "Seeds" TEXT NOT NULL,
    "Takes" TEXT NOT NULL,
    "Warnings" TEXT NOT NULL
);

CREATE TABLE "Styles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Styles" PRIMARY KEY,
    "Kind" TEXT NOT NULL,
    "PresetKey" TEXT NULL,
    "Name" TEXT NOT NULL,
    "NormalizedName" TEXT NOT NULL,
    "GenreFamily" TEXT NOT NULL,
    "SubGenre" TEXT NULL,
    "TempoFeel" TEXT NOT NULL,
    "VocalHandling" TEXT NOT NULL,
    "DefaultStrength" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "Instrumentation" TEXT NOT NULL,
    "Moods" TEXT NOT NULL
);

CREATE INDEX "IX_Generations_CreatedAt" ON "Generations" ("CreatedAt");

CREATE INDEX "IX_Generations_SourceTrackId" ON "Generations" ("SourceTrackId");

CREATE INDEX "IX_Generations_Status" ON "Generations" ("Status");

CREATE UNIQUE INDEX "IX_Styles_NormalizedName" ON "Styles" ("NormalizedName");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115190101_AddGenerativeStyles', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Generations" ADD "Song" TEXT NULL;

ALTER TABLE "Generations" ADD "VoiceConversion" TEXT NULL;

ALTER TABLE "Generations" ADD "VoiceProfileId" TEXT NULL;

CREATE TABLE "AudioHashes" (
    "Sha256" TEXT NOT NULL CONSTRAINT "PK_AudioHashes" PRIMARY KEY,
    "Kind" TEXT NOT NULL,
    "OwnerId" TEXT NOT NULL
);

CREATE TABLE "LyricsSheets" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_LyricsSheets" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Language" TEXT NOT NULL,
    "Text" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE "VoiceProfiles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_VoiceProfiles" PRIMARY KEY,
    "DisplayName" TEXT NOT NULL,
    "NormalizedName" TEXT NOT NULL,
    "Consent" TEXT NOT NULL,
    "SampleSource" TEXT NOT NULL,
    "SamplePath" TEXT NOT NULL,
    "SamplePcmSha256" TEXT NOT NULL,
    "SampleFileSha256" TEXT NOT NULL,
    "DurationSeconds" REAL NOT NULL,
    "VoicedRatio" REAL NOT NULL,
    "CreatedAt" TEXT NOT NULL
);

CREATE INDEX "IX_Generations_VoiceProfileId" ON "Generations" ("VoiceProfileId");

CREATE INDEX "IX_AudioHashes_OwnerId" ON "AudioHashes" ("OwnerId");

CREATE INDEX "IX_LyricsSheets_UpdatedAt" ON "LyricsSheets" ("UpdatedAt");

CREATE UNIQUE INDEX "IX_VoiceProfiles_NormalizedName" ON "VoiceProfiles" ("NormalizedName");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115200748_AddLyricsAndVoices', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "AgentSettings" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AgentSettings" PRIMARY KEY,
    "Backend" TEXT NOT NULL,
    "OllamaModel" TEXT NOT NULL,
    "OllamaFallbackModel" TEXT NOT NULL,
    "OllamaThink" INTEGER NOT NULL,
    "ClaudeModel" TEXT NOT NULL,
    "ClaudeEffort" TEXT NOT NULL,
    "ClaudeNoticeAcknowledgedAt" TEXT NULL,
    "Limits" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE "Conversations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Conversations" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "ActiveRunId" TEXT NULL
);

CREATE TABLE "AgentRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AgentRuns" PRIMARY KEY,
    "ConversationId" TEXT NOT NULL,
    "UserMessageId" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Backend" TEXT NOT NULL,
    "Model" TEXT NOT NULL,
    "Tainted" INTEGER NOT NULL,
    "Plan" TEXT NOT NULL,
    "ModelTurns" INTEGER NOT NULL,
    "ToolCalls" INTEGER NOT NULL,
    "InvalidCalls" INTEGER NOT NULL,
    "InputTokens" INTEGER NOT NULL,
    "OutputTokens" INTEGER NOT NULL,
    "CachedInputTokens" INTEGER NOT NULL,
    "FailureCode" TEXT NULL,
    "FailureMessage" TEXT NULL,
    "StartedAt" TEXT NOT NULL,
    "EndedAt" TEXT NULL,
    CONSTRAINT "FK_AgentRuns_Conversations_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "Conversations" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ConversationMessages" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ConversationMessages" PRIMARY KEY,
    "ConversationId" TEXT NOT NULL,
    "Seq" INTEGER NOT NULL,
    "Role" TEXT NOT NULL,
    "Text" TEXT NOT NULL,
    "Page" TEXT NOT NULL,
    "TrackId" TEXT NULL,
    "MixId" TEXT NULL,
    "RunId" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_ConversationMessages_Conversations_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "Conversations" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Approvals" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Approvals" PRIMARY KEY,
    "RunId" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Scope" TEXT NOT NULL,
    "ArgumentsHash" TEXT NOT NULL,
    "RequestedAt" TEXT NOT NULL,
    "DecidedAt" TEXT NULL,
    CONSTRAINT "FK_Approvals_AgentRuns_RunId" FOREIGN KEY ("RunId") REFERENCES "AgentRuns" ("Id") ON DELETE CASCADE
);

CREATE TABLE "RunEvents" (
    "RunId" TEXT NOT NULL,
    "Seq" INTEGER NOT NULL,
    "Type" TEXT NOT NULL,
    "Payload" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "PK_RunEvents" PRIMARY KEY ("RunId", "Seq"),
    CONSTRAINT "FK_RunEvents_AgentRuns_RunId" FOREIGN KEY ("RunId") REFERENCES "AgentRuns" ("Id") ON DELETE CASCADE
);

CREATE TABLE "RunSteps" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_RunSteps" PRIMARY KEY,
    "RunId" TEXT NOT NULL,
    "Seq" INTEGER NOT NULL,
    "CallId" TEXT NOT NULL,
    "Tool" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "Risk" TEXT NOT NULL,
    "UndoKind" TEXT NOT NULL,
    "Arguments" TEXT NOT NULL,
    "ArgumentsHash" TEXT NOT NULL,
    "PlanIndex" INTEGER NULL,
    "Decision" TEXT NOT NULL,
    "DecisionReasons" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "StartedAt" TEXT NULL,
    "EndedAt" TEXT NULL,
    "EstimatedMinutes" REAL NULL,
    "ResultSummary" TEXT NULL,
    "Error" TEXT NULL,
    "Progress" INTEGER NULL,
    "JobTrackId" TEXT NULL,
    "JobId" TEXT NULL,
    "JobPhase" TEXT NULL,
    CONSTRAINT "FK_RunSteps_AgentRuns_RunId" FOREIGN KEY ("RunId") REFERENCES "AgentRuns" ("Id") ON DELETE CASCADE
);

CREATE TABLE "UndoEntries" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_UndoEntries" PRIMARY KEY,
    "ConversationId" TEXT NOT NULL,
    "RunId" TEXT NOT NULL,
    "StepId" TEXT NOT NULL,
    "Ordinal" INTEGER NOT NULL,
    "Kind" TEXT NOT NULL,
    "ActionTool" TEXT NOT NULL,
    "ActionArguments" TEXT NOT NULL,
    "Fresh" INTEGER NOT NULL,
    "ExpectedUpdatedAt" TEXT NULL,
    "Status" TEXT NOT NULL,
    CONSTRAINT "FK_UndoEntries_AgentRuns_RunId" FOREIGN KEY ("RunId") REFERENCES "AgentRuns" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_AgentRuns_ConversationId_StartedAt" ON "AgentRuns" ("ConversationId", "StartedAt");

CREATE INDEX "IX_AgentRuns_Status" ON "AgentRuns" ("Status");

CREATE INDEX "IX_Approvals_RunId_Status" ON "Approvals" ("RunId", "Status");

CREATE UNIQUE INDEX "IX_ConversationMessages_ConversationId_Seq" ON "ConversationMessages" ("ConversationId", "Seq");

CREATE INDEX "IX_Conversations_UpdatedAt" ON "Conversations" ("UpdatedAt");

CREATE UNIQUE INDEX "IX_RunSteps_RunId_Seq" ON "RunSteps" ("RunId", "Seq");

CREATE INDEX "IX_UndoEntries_ConversationId_Ordinal" ON "UndoEntries" ("ConversationId", "Ordinal");

CREATE INDEX "IX_UndoEntries_RunId" ON "UndoEntries" ("RunId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116064402_AddAgent', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
CREATE TABLE "AgentSecrets" (
    "Name" TEXT NOT NULL CONSTRAINT "PK_AgentSecrets" PRIMARY KEY,
    "Ciphertext" TEXT NOT NULL,
    "LastFour" TEXT NOT NULL,
    "PurposeVersion" INTEGER NOT NULL,
    "StoredAt" TEXT NOT NULL,
    CONSTRAINT "CK_AgentSecrets_Ciphertext" CHECK (length("Ciphertext") BETWEEN 1 AND 2048),
    CONSTRAINT "CK_AgentSecrets_LastFour" CHECK (length("LastFour") = 4),
    CONSTRAINT "CK_AgentSecrets_Name" CHECK (length("Name") BETWEEN 1 AND 40)
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116165940_AddAgentSecrets', '10.0.15');

COMMIT;

BEGIN TRANSACTION;
ALTER TABLE "Mixes" ADD "Practice" TEXT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116211814_AddMixPractice', '10.0.15');

COMMIT;

