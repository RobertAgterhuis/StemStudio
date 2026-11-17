using System.Text.Json;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.LibraryCare;

/// <summary>
/// The state and rules of the restore dialog (013 US2, FR-012-FR-018): mode, voice profiles, the typed confirmation,
/// the space check (unpacked size + 256 MiB against the library volume) and the blockers (active jobs, a kept previous
/// library). The API checks everything again; the form only keeps Start off while it would be refused.
/// </summary>
/// <param name="status">The library care status when the dialog opened.</param>
public sealed class RestoreForm(LibraryCareStatusDto status)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>True when the library is empty: no mode, no confirmation.</summary>
    public bool Empty { get; } = status.LibraryEmpty;

    /// <summary>The chosen mode.</summary>
    public RestoreModeDto Mode { get; set; } = RestoreModeDto.Replace;

    /// <summary>"Restore voice profiles".</summary>
    public bool VoiceProfiles { get; set; } = true;

    /// <summary>The typed confirmation.</summary>
    public string Confirmation { get; set; } = string.Empty;

    /// <summary>The jobs that block the restore (from the status, or from a <c>jobs-active</c> answer).</summary>
    public IReadOnlyList<ActiveWorkDto> Blockers { get; private set; } = status.ActiveWork;

    /// <summary>The archive details, once loaded.</summary>
    public BackupDetailsDto? Details { get; set; }

    /// <summary>Free bytes of the library volume, or null when unknown.</summary>
    public long? FreeBytes { get; set; }

    /// <summary>True when a replace on a non-empty library must be confirmed by typing <c>REPLACE</c>.</summary>
    public bool NeedsConfirmation => !Empty && Mode == RestoreModeDto.Replace;

    /// <summary>True when the archive contains voice profiles (the checkbox is shown).</summary>
    public bool HasVoiceProfiles => Details is { } details && (details.VoiceProfiles.Count > 0 || details.Counts?.VoiceProfiles > 0);

    /// <summary>The space a restore needs, or null before the details are loaded.</summary>
    public long? NeededBytes => Details is { } details ? details.TotalBytes + LibraryCareLimits.RestoreFreeMarginBytes : null;

    /// <summary>False only when both sizes are known and the free space is too small.</summary>
    public bool Enough => NeededBytes is not { } needed || FreeBytes is not { } free || free >= needed;

    /// <summary>Why a replace cannot start while a previous library is kept, or null.</summary>
    public string? PreviousBlocked => !Empty && Mode == RestoreModeDto.Replace && status.PreviousLibrary is not null
        ? "A previous library is still kept. Undo the last restore or discard the previous library first."
        : null;

    /// <summary>True when every rule allows the restore.</summary>
    public bool CanStart => Details is not null && Blockers.Count == 0 && Enough && PreviousBlocked is null
        && (!NeedsConfirmation || Confirmation == LibraryCareLimits.ReplaceConfirmation);

    /// <summary>The request body.</summary>
    public RestoreOptionsDto Options => new()
    {
        Mode = Empty ? RestoreModeDto.Replace : Mode,
        VoiceProfiles = VoiceProfiles,
        Confirmation = NeedsConfirmation ? Confirmation : null,
    };

    /// <summary>The space line: "Needs about 3.3 GB; 20 GB free".</summary>
    public string SpaceText => NeededBytes is not { } needed ? "Checking the size..."
        : FreeBytes is { } free ? $"Needs about {ByteSize.Format(needed)}; {ByteSize.Format(free)} free in the library{(Enough ? "." : ": not enough space.")}"
        : $"Needs about {ByteSize.Format(needed)}; the free space of the library is not known.";

    /// <summary>The page where a blocking job can be followed or cancelled; null for an agent run (composer panel).</summary>
    /// <param name="work">The job.</param>
    /// <returns>A relative link, or null.</returns>
    public static string? BlockerLink(ActiveWorkDto work) => work.Kind switch
    {
        ActiveWorkKindDto.Export => "library",
        ActiveWorkKindDto.Generation => "generations",
        ActiveWorkKindDto.AgentRun => null,
        _ => Guid.TryParse(work.Id, out var trackId) ? $"tracks/{trackId}" : "tracks",
    };

    /// <summary>The kind of a blocking job as text.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>For example "Export".</returns>
    public static string BlockerKind(ActiveWorkKindDto kind) => kind switch
    {
        ActiveWorkKindDto.Import => "Import",
        ActiveWorkKindDto.Detail => "Split further",
        ActiveWorkKindDto.Export => "Export",
        ActiveWorkKindDto.Score => "Sheet music",
        ActiveWorkKindDto.LyricsAlignment => "Lyrics alignment",
        ActiveWorkKindDto.Generation => "Generation",
        _ => "Composer agent run",
    };

    /// <summary>Takes the <c>activeWork</c> list of a <c>jobs-active</c> answer as the blockers.</summary>
    /// <param name="error">The refused restore, or null when it was accepted.</param>
    public void TakeBlockers(ApiError? error)
    {
        if (error?.Code == LibraryCareProblemTypes.JobsActive && error.Extensions.TryGetValue("activeWork", out var work))
        {
            Blockers = work.Deserialize<List<ActiveWorkDto>>(Json) ?? [];
        }
    }
}
