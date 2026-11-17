using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.Tests.Contract;

/// <summary>DTO instances of the 1.12.0 schemas that have no contract example (013 Phase 0, T006).</summary>
public sealed partial class LibraryCareContractTests
{
    private static class Samples
    {
        private static readonly DateTimeOffset At = new(2026, 11, 16, 14, 22, 33, TimeSpan.Zero);

        public static ArchiveCountsDto Counts { get; } = new()
        {
            Tracks = 3,
            Mixes = 2,
            Assets = 2,
            Generations = 1,
            Styles = 1,
            LyricsSheets = 1,
            VoiceProfiles = 1,
        };

        public static VerifyReportDto Verify { get; } = new()
        {
            Ok = false,
            EntriesChecked = 12,
            BytesChecked = 4096,
            Problem = new LibraryJobErrorDto { Code = LibraryJobErrorCodeDto.ArchiveCorrupt, Message = "Entry 12 does not match its SHA-256." },
        };

        public static PreviousLibraryDto Previous { get; } = new()
        {
            RestoreId = Guid.NewGuid(),
            MovedAsideAt = At,
            SizeBytes = 1024,
            Counts = Counts,
            RestoredArchive = "stemstudio-backup-20261116-142233.zip",
        };

        public static CleanupResultDto CleanupResult { get; } = new()
        {
            PlanId = Guid.NewGuid(),
            FreedBytes = 400,
            Removed = [new CleanupRemovedDto { Kind = CleanupKindDto.WorkerCache, TrackId = null, Files = 2, Bytes = 400 }],
            Failed = [new CleanupSkipDto { Kind = CleanupKindDto.DetailStems, TrackId = Guid.NewGuid(), Title = "Song", Reason = CleanupSkipReasonDto.ExportActive }],
        };

        public static BackupDetailsDto Details { get; } = new()
        {
            FileName = "stemstudio-backup-20261116-142233.zip",
            SizeBytes = 412339200,
            ModifiedAt = At,
            Recognised = true,
            Reason = null,
            BackupId = Guid.NewGuid(),
            CreatedAt = At,
            AppVersion = "1.12.0+5ae61cf",
            BackendApi = "1.12.0",
            FormatVersion = LibraryCareLimits.ArchiveFormatVersion,
            Included = [BackupCategoryDto.Database, BackupCategoryDto.Stems, BackupCategoryDto.VoiceProfiles],
            Counts = Counts,
            Compatibility = new CompatibilityDto { Restorable = true, Reason = null, MigrationNeeded = true },
            Excluded = [BackupCategoryDto.DetailStems],
            Entries = 25,
            TotalBytes = 412339200,
            SchemaLastMigration = "20261115200748_AddLyricsAndVoices",
            VoiceProfiles =
            [
                new VoiceProfileProofDto
                {
                    Id = Guid.NewGuid(), DisplayName = "My voice", StatementVersion = "consent.v1", PersonName = "Alex", ConsentedAt = At, Valid = true, Reason = null,
                },
            ],
            Warnings = [new ArchiveWarningDto { Code = ArchiveWarningCodeDto.MissingFile, Message = "1 file was missing." }],
        };
    }
}
