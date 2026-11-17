using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Library care DTOs of backend API 1.12.0 (feature 013) for the fake <c>ILibraryCareClient</c>.</summary>
public static class LibraryCareData
{
    public const string Archive = "stemstudio-backup-20261117-101500.zip";

    public const string OlderArchive = "stemstudio-backup-20261101-080000.zip";

    public static readonly Guid JobId = Guid.Parse("8e0c1f2a-3b4d-4e5f-8a6b-7c8d9e0f1a2b");

    public static readonly Guid ProfileId = Guid.Parse("a1b2c3d4-0000-4000-8000-0000000000f1");

    public static readonly Guid RestoreId = Guid.Parse("b1b2c3d4-0000-4000-8000-0000000000e1");

    public static readonly DateTimeOffset Made = new(2026, 11, 17, 10, 15, 0, TimeSpan.Zero);

    public static FolderStateDto Folder(bool available = true, bool writable = true, long? free = 120L * 1024 * 1024 * 1024, string? reason = null) => new()
    {
        Available = available,
        Writable = writable,
        HostPathHint = "./backups",
        FreeBytes = free,
        TotalBytes = 500L * 1024 * 1024 * 1024,
        Reason = reason,
    };

    public static LibraryCareStatusDto Status(
        LibraryJobDto? active = null, GateModeDto gate = GateModeDto.Open, PreviousLibraryDto? previous = null, bool empty = false,
        IReadOnlyList<ActiveWorkDto>? work = null) => new()
    {
        Gate = gate,
        ActiveJob = active,
        BackupFolder = Folder(),
        PreviousLibrary = previous,
        ActiveWork = work ?? [],
        LibraryEmpty = empty,
    };

    public static ArchiveCountsDto Counts(int tracks = 3, int voices = 1) => new()
    {
        Tracks = tracks, Mixes = 1, Assets = 2, Generations = 1, Styles = 0, LyricsSheets = 0, VoiceProfiles = voices,
    };

    public static CompatibilityDto Compatible(bool restorable = true, bool migration = false, string? reason = null) =>
        new() { Restorable = restorable, MigrationNeeded = migration, Reason = reason };

    public static BackupSummaryDto Summary(string fileName = Archive, CompatibilityDto? compatibility = null, bool recognised = true) => new()
    {
        FileName = fileName,
        SizeBytes = 2L * 1024 * 1024 * 1024,
        ModifiedAt = Made,
        Recognised = recognised,
        Reason = recognised ? null : "no manifest.json",
        BackupId = recognised ? Guid.Parse("c1b2c3d4-0000-4000-8000-0000000000a1") : null,
        CreatedAt = recognised ? Made : null,
        AppVersion = recognised ? "1.13.0" : null,
        BackendApi = recognised ? "1.12.0" : null,
        FormatVersion = recognised ? "1.0.0" : null,
        Included = recognised ? [BackupCategoryDto.Database, BackupCategoryDto.Stems, BackupCategoryDto.VoiceProfiles] : [],
        Counts = recognised ? Counts() : null,
        Compatibility = compatibility ?? Compatible(recognised),
    };

    public static BackupListDto List(params BackupSummaryDto[] archives) => new() { Folder = Folder(), Archives = archives };

    public static BackupDetailsDto Details(string fileName = Archive, bool validProof = true) => new()
    {
        FileName = fileName,
        SizeBytes = 2L * 1024 * 1024 * 1024,
        ModifiedAt = Made,
        Recognised = true,
        Reason = null,
        BackupId = Guid.Parse("c1b2c3d4-0000-4000-8000-0000000000a1"),
        CreatedAt = Made,
        AppVersion = "1.13.0",
        BackendApi = "1.12.0",
        FormatVersion = "1.0.0",
        Included = [BackupCategoryDto.Database, BackupCategoryDto.Stems, BackupCategoryDto.VoiceProfiles],
        Counts = Counts(),
        Compatibility = Compatible(),
        Excluded = [BackupCategoryDto.DetailStems],
        Entries = 412,
        TotalBytes = 3L * 1024 * 1024 * 1024,
        SchemaLastMigration = "20261116000000_LibraryCare",
        VoiceProfiles =
        [
            new VoiceProfileProofDto
            {
                Id = ProfileId, DisplayName = "My voice", StatementVersion = "consent.v1", PersonName = "Sam Example",
                ConsentedAt = new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero), Valid = validProof,
                Reason = validProof ? null : "statement hash mismatch",
            },
        ],
        Warnings = [new ArchiveWarningDto { Code = ArchiveWarningCodeDto.MissingFile, Message = "1 file was missing" }],
    };

    public static CategorySizeDto Size(BackupCategoryDto category, long bytes, int files, bool included = true, bool optional = true,
        RegenerabilityDto regenerability = RegenerabilityDto.Original) =>
        new() { Category = category, Bytes = bytes, Files = files, Included = included, Optional = optional, Regenerability = regenerability };

    public static BackupEstimateDto Estimate(bool enough = true, bool detail = true, bool voices = true, int voiceFiles = 1) => new()
    {
        TotalBytes = 21L * 1024 * 1024 * 1024,
        Folder = Folder(),
        Enough = enough,
        Categories =
        [
            Size(BackupCategoryDto.Database, 4_000_000, 1, optional: false),
            Size(BackupCategoryDto.Stems, 800_000_000, 24, optional: false),
            Size(BackupCategoryDto.DetailStems, 600_000_000, 12, detail, regenerability: RegenerabilityDto.Regenerable),
            Size(BackupCategoryDto.VoiceProfiles, 2_000_000, voiceFiles, voices),
        ],
    };

    public static LibraryJobDto Job(
        LibraryJobKindDto kind = LibraryJobKindDto.Backup, LibraryJobStatusDto status = LibraryJobStatusDto.Running,
        LibraryJobStageDto? stage = LibraryJobStageDto.Files, int progress = 42, LibraryJobReportDto? report = null,
        LibraryJobErrorDto? error = null, bool cancellable = true, Guid? id = null) => new()
    {
        JobId = id ?? JobId,
        Kind = kind,
        Status = status,
        Stage = stage,
        StageText = null,
        Progress = progress,
        BytesDone = 900L * 1024 * 1024,
        BytesTotal = 2L * 1024 * 1024 * 1024,
        CurrentItem = "Song title - vocals.flac",
        EtaSeconds = 180,
        ArchiveFileName = Archive,
        Cancellable = cancellable,
        Report = report,
        Error = error,
        CreatedAt = Made,
        StartedAt = Made,
        FinishedAt = null,
    };

    public static BackupReportDto BackupReport() => new()
    {
        FileName = Archive,
        Entries = 412,
        Bytes = 2L * 1024 * 1024 * 1024,
        Included = [new CategoryCountDto { Category = BackupCategoryDto.Stems, Items = 3, Files = 24, Bytes = 800L * 1024 * 1024 }],
        Warnings = [new ArchiveWarningDto { Code = ArchiveWarningCodeDto.MissingFile, Message = "Artwork of \"Song\" was missing" }],
    };

    public static LibraryJobDto CompletedBackup() =>
        Job(status: LibraryJobStatusDto.Completed, stage: null, progress: 100, report: BackupReport(), cancellable: false);

    public static PreviousLibraryDto Previous() => new()
    {
        RestoreId = RestoreId,
        MovedAsideAt = Made,
        SizeBytes = 5L * 1024 * 1024 * 1024,
        Counts = Counts(tracks: 7, voices: 0),
        RestoredArchive = Archive,
    };
}
