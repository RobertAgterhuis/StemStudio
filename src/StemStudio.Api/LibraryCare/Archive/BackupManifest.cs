using System.Text.Json;
using System.Text.Json.Serialization;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.LibraryCare;

namespace StemStudio.Api.LibraryCare.Archive;

/// <summary><c>manifest.json</c> of a backup archive (backup-archive 1.0.0 section 3, <c>backup-manifest.schema.json</c>).</summary>
public sealed record BackupManifest
{
    /// <summary>Always <see cref="LibraryCareLimits.ArchiveFormat"/>.</summary>
    public string Format { get; init; } = LibraryCareLimits.ArchiveFormat;

    /// <summary>Format version (readers accept 1.x).</summary>
    public string FormatVersion { get; init; } = LibraryCareLimits.ArchiveFormatVersion;

    /// <summary>Random id of this backup (lower-case D format).</summary>
    public required string BackupId { get; init; }

    /// <summary>UTC time of the database snapshot.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>The writing application.</summary>
    public required ManifestApp App { get; init; }

    /// <summary>The database schema.</summary>
    public required ManifestSchema Schema { get; init; }

    /// <summary>Included and excluded categories.</summary>
    public required ManifestContents Contents { get; init; }

    /// <summary>Rows per entity type in the archived database.</summary>
    public required ManifestCounts Counts { get; init; }

    /// <summary>Entry count and uncompressed bytes of <see cref="Entries"/>.</summary>
    public required ManifestTotals Totals { get; init; }

    /// <summary>Always <c>sha-256</c>.</summary>
    public string HashAlgorithm { get; init; } = "sha-256";

    /// <summary>Always <c>none</c>.</summary>
    public string Encryption { get; init; } = "none";

    /// <summary>Every entry except the manifest and the README, in archive order.</summary>
    public required IReadOnlyList<ManifestEntry> Entries { get; init; }

    /// <summary>Consent proof per archived voice profile.</summary>
    public required IReadOnlyList<ManifestVoiceProfile> VoiceProfiles { get; init; }

    /// <summary>Warnings of the backup.</summary>
    public required IReadOnlyList<ArchiveWarningDto> Warnings { get; init; }
}

/// <summary>JSON options of the manifest: camelCase members and enum values, nulls omitted.</summary>
public static class ManifestJson
{
    /// <summary>The options.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

/// <summary>The writing application.</summary>
/// <param name="Name">Always <c>StemStudio</c>.</param>
/// <param name="Version">Informational version of the API assembly.</param>
/// <param name="BackendApi">Backend API contract version.</param>
public sealed record ManifestApp(string Name, string Version, string BackendApi);

/// <summary>The database schema of the archive.</summary>
/// <param name="Provider">Always <c>sqlite</c>.</param>
/// <param name="LastMigration">Last row of <c>__EFMigrationsHistory</c>.</param>
/// <param name="Migrations">Every row in order.</param>
public sealed record ManifestSchema(string Provider, string LastMigration, IReadOnlyList<string> Migrations);

/// <summary>Why a category is not in the archive.</summary>
public enum ExclusionReason
{
    /// <summary>Left out by an option.</summary>
    Option,

    /// <summary>Nothing of it exists.</summary>
    Empty,
}

/// <summary>An excluded category.</summary>
/// <param name="Category">The category.</param>
/// <param name="Reason">Why.</param>
public sealed record ManifestExclusion(BackupCategory Category, ExclusionReason Reason);

/// <summary>Included and excluded categories.</summary>
/// <param name="Included">Categories with entries in the archive.</param>
/// <param name="Excluded">Every other category with its reason.</param>
public sealed record ManifestContents(IReadOnlyList<BackupCategory> Included, IReadOnlyList<ManifestExclusion> Excluded);

/// <summary>Rows per entity type (styles: user styles only).</summary>
public sealed record ManifestCounts
{
    /// <summary>Tracks.</summary>
    public int Tracks { get; init; }

    /// <summary>Mixes.</summary>
    public int Mixes { get; init; }

    /// <summary>Library items.</summary>
    public int Assets { get; init; }

    /// <summary>Generations.</summary>
    public int Generations { get; init; }

    /// <summary>User styles.</summary>
    public int Styles { get; init; }

    /// <summary>Lyrics sheets.</summary>
    public int LyricsSheets { get; init; }

    /// <summary>Voice profiles.</summary>
    public int VoiceProfiles { get; init; }

    /// <summary>Artists.</summary>
    public int Artists { get; init; }

    /// <summary>Agent conversations.</summary>
    public int AgentConversations { get; init; }
}

/// <summary>Entry count and bytes.</summary>
/// <param name="Entries">Number of entries.</param>
/// <param name="Bytes">Uncompressed bytes.</param>
public sealed record ManifestTotals(int Entries, long Bytes);

/// <summary>The entity an entry belongs to.</summary>
/// <param name="Type"><c>track</c>, <c>asset</c>, <c>generation</c>, <c>voiceProfile</c> or <c>upload</c>.</param>
/// <param name="Id">Its id.</param>
public sealed record ManifestEntity(string Type, string Id);

/// <summary>One archive entry.</summary>
/// <param name="Path">Entry name.</param>
/// <param name="Category">Category.</param>
/// <param name="SizeBytes">Uncompressed size.</param>
/// <param name="Sha256">SHA-256 of the content.</param>
/// <param name="Entity">Owning entity, if any.</param>
public sealed record ManifestEntry(string Path, BackupCategory Category, long SizeBytes, string Sha256, ManifestEntity? Entity);

/// <summary>The consent record of a voice profile in the manifest.</summary>
/// <param name="StatementVersion">Statement version.</param>
/// <param name="StatementSha256">Statement hash.</param>
/// <param name="PersonName">Person name.</param>
/// <param name="ConsentedAt">When (UTC).</param>
public sealed record ManifestConsent(string StatementVersion, string StatementSha256, string PersonName, DateTime ConsentedAt);

/// <summary>Consent proof of an archived voice profile.</summary>
/// <param name="Id">Profile id.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Consent">The consent record.</param>
/// <param name="SamplePath">Entry name of the sample.</param>
/// <param name="SampleSha256">SHA-256 of the sample.</param>
public sealed record ManifestVoiceProfile(string Id, string DisplayName, ManifestConsent Consent, string SamplePath, string SampleSha256)
{
    /// <summary>The proof as the domain compares it.</summary>
    /// <returns>The proof.</returns>
    public ConsentProof ToProof() => new(Consent.StatementVersion, Consent.StatementSha256, Consent.PersonName, Consent.ConsentedAt, SampleSha256);
}
