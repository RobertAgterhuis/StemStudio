namespace StemStudio.Domain.Generations;

/// <summary>Songs and voice conversions (feature 010, data-model 3.2).</summary>
public sealed partial class Generation
{
    /// <summary>Suffix of a deleted profile's name snapshot.</summary>
    public const string ProfileDeletedSuffix = " (profile deleted)";

    /// <summary>Song details (kind song).</summary>
    public SongDetails? Song { get; private set; }

    /// <summary>Voice-conversion details (kind voice conversion).</summary>
    public VoiceConversionDetails? VoiceConversion { get; private set; }

    /// <summary>The voice profile of a conversion (copied for queries); null after it was deleted.</summary>
    public Guid? VoiceProfileId { get; private set; }

    /// <summary>Creates a queued song.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="details">Lyrics, style, voice and options.</param>
    /// <param name="seeds">One seed per take.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The generation.</returns>
    public static Generation CreateSong(Guid id, SongDetails details, IReadOnlyList<int> seeds, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(details);
        var generation = New(id, GenerationKind.Song, seeds, nowUtc);
        generation.Song = details;
        return generation;
    }

    /// <summary>Creates a queued voice conversion (one take, seed 0).</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="details">Source take and profile.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The generation.</returns>
    public static Generation CreateVoiceConversion(Guid id, VoiceConversionDetails details, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(details);
        var generation = New(id, GenerationKind.VoiceConversion, [0], nowUtc);
        (generation.VoiceConversion, generation.VoiceProfileId) = (details, details.VoiceProfileId);
        return generation;
    }

    /// <summary>Adds warnings (merged, at most 5), e.g. a lyrics-density warning at creation.</summary>
    /// <param name="warnings">The warnings.</param>
    public void AddWarnings(IEnumerable<string> warnings) =>
        _warnings = _warnings.Union(warnings, StringComparer.Ordinal).Take(MaxWarnings).ToList();

    /// <summary>The conversion waits until the imported take is separated (API-only status).</summary>
    /// <param name="importedTrackId">The imported track.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void WaitForStems(Guid importedTrackId, DateTime nowUtc)
    {
        RequireActive();
        VoiceConversion = VoiceConversion! with { ImportedTrackId = importedTrackId };
        (Status, Stage, UpdatedAt) = (GenerationState.WaitingForStems, "Separating the take into stems", nowUtc);
    }

    /// <summary>Forgets a deleted voice profile; the name snapshot stays with "(profile deleted)".</summary>
    /// <param name="profileId">The deleted profile.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>True when this conversion used it.</returns>
    public bool DetachVoiceProfile(Guid profileId, DateTime nowUtc)
    {
        if (VoiceConversion?.VoiceProfileId != profileId)
        {
            return false;
        }

        var name = VoiceConversion.VoiceProfileName + ProfileDeletedSuffix;
        (VoiceConversion, VoiceProfileId) = (VoiceConversion with { VoiceProfileId = null, VoiceProfileName = name }, null);
        _takes = _takes.Select(take => take with { VoiceProfileName = take.VoiceProfileName is null ? null : name }).ToList();
        UpdatedAt = nowUtc;
        return true;
    }
}
