namespace StemStudio.Web.Experience;

/// <summary>
/// The feature catalogue (design-spec.md 5, FR-013/FR-014): every level-dependent control group or aid with
/// the levels that show it and the help topic that explains it. Data, not branching: components ask
/// <see cref="IsVisible"/> through <c>LevelGate</c>. Everything not listed is shown at every level.
/// Later features add entries here (FR-028).
/// </summary>
public static class FeatureCatalog
{
    private const ExperienceLevel Beginner = ExperienceLevel.Beginner;
    private const ExperienceLevel Advanced = ExperienceLevel.Advanced;
    private const ExperienceLevel Expert = ExperienceLevel.Expert;

    private static readonly Dictionary<string, UiFeature> ById = new UiFeature[]
    {
        new("mixer.pan", Advanced, null, "mixer-pan"),
        new("mixer.pan-marker", Beginner, Beginner, "mixer-pan"),
        new("mixer.loop.typed", Advanced, null, "mixer-loop"),
        new("mixer.metronome.advanced", Advanced, null, "mixer-metronome"),
        new("mixer.numeric", Expert, null, "mixer-numeric-entry"),
        new("mixer.meter.db", Expert, null, "mixer-meters"),
        new("mixer.presets", Beginner, null, "mixer-presets"),
        new("practice.speed-reset", Advanced, null, "practice-speed"),
        new("practice.trainer", Advanced, null, "practice-speed-trainer"),
        new("practice.count-in.options", Advanced, null, "practice-count-in"),
        new("practice.numeric", Expert, null, "practice-speed"),
        new("practice.stretch-status", Expert, null, "practice-sound-quality"),
        new("tracks.search", Advanced, null, "tracks-library"),
        new("mixes.search", Advanced, null, "mixes-library"),
        new("track.job-details", Expert, null, "track-job-details"),
        new("artist.details", Advanced, null, "band-info"),
        new("artwork.own-picture", Advanced, null, "artwork"),
        new("export.format-details", Advanced, null, "export-formats"),
        new("export.selected-channels", Advanced, null, "export-dialog"),
        new("library.filters", Advanced, null, "library"),
        new("export.range", Advanced, null, "export-range"),
        new("export.stem-pack", Advanced, null, "export-dialog"),
        new("karaoke.size", Advanced, null, "karaoke"),
        new("karaoke.offset", Advanced, null, "karaoke-offset"),
        new("karaoke.timing-origin", Expert, null, "karaoke"),
        new("lyrics.align-device", Advanced, null, "lyrics-align"),
        new("lyrics.refine", Expert, null, "lyrics-align"),
        new("lyrics.remove", Advanced, null, "lyrics-sources"),
        new("score.detail-stems", Advanced, null, "sheet-music"),
        new("score.key-override", Advanced, null, "sheet-music-key-tempo"),
        new("score.manual-tempo", Advanced, null, "sheet-music-key-tempo"),
        new("score.quality", Advanced, null, "sheet-music-piano"),
        new("score.range", Advanced, null, "sheet-music"),
        new("restyle.options", Advanced, null, "restyle-options"),
        new("song.options", Advanced, null, "song-options"),
        new("voice.pitch", Advanced, null, "voice-conversion"),
        new("shell.shortcut-overlay-key", Expert, null, "shortcuts"),
        new("shell.kbd-hints", Expert, null, "shortcuts"),
        new("journey.rail", Beginner, Beginner, "journey"),
        new("help.inline-hints", Beginner, Beginner, "levels"),
        new("help.tour-offers", Beginner, Expert, "tours"),
        new("agent.steps", Advanced, null, "agent-panel"),
        new("agent.steps-compact", Beginner, Beginner, "agent-panel"),
        new("agent.conversation-actions", Advanced, null, "agent-panel"),
        new("agent.usage", Advanced, null, "agent-backends"),
        new("agent.tool-log", Expert, null, "agent-panel"),
        new("agent.models", Advanced, null, "agent-backends"),
        new("agent.claude-options", Expert, null, "agent-backends"),
        new("care.backup-options", Advanced, null, "backup-contents"),
        new("care.job-bytes", Advanced, null, "library-care"),
        new("care.verify", Advanced, null, "backup-compatibility"),
        new("care.manifest", Expert, null, "backup-contents"),
        new("care.track-table", Advanced, null, "storage-overview"),
        new("care.top-tracks", Beginner, Beginner, "storage-overview"),
        new("care.worker-entries", Expert, null, "storage-regenerable"),
        new("care.restore-merge", Advanced, null, "backup-restore"),
        new("lyrics.assist-count", Advanced, null, "lyric-assist"),
        new("lyrics.assist-titles", Advanced, null, "lyric-assist"),
        new("lyrics.assist-filters", Advanced, null, "lyric-assist"),
        new("lyrics.assist-layers", Advanced, null, "lyric-assist-privacy"),
        new("lyrics.assist-usage", Advanced, null, "lyric-assist-privacy"),
        new("lyrics.assist-sensitivity", Advanced, null, "lyric-assist-licence"),
        new("lyrics.assist-marks", Advanced, null, "lyric-assist-licence"),
        new("lyrics.assist-rhythm", Advanced, null, "lyric-assist-rhythm"),
        new("lyrics.assist-source", Expert, null, "lyric-assist-privacy"),
        new("lyrics.assist-log", Expert, null, "lyric-assist-privacy"),
        new("lyrics.assist-revisions", Expert, null, "lyric-assist"),
        new("shortcuts.settings", Advanced, null, "shortcuts-custom"),
        new("sections.edit", Advanced, null, "mixer-sections-edit"),
        new("setlists.notes", Advanced, null, "setlists"),
        new("practice.log-sections", Advanced, null, "mixer-practice-log"),
        new("practice.log-tempo", Advanced, null, "mixer-practice-log"),
        new("practice.sessions", Expert, null, "practice-privacy"),
        new("setlists.pdf-options", Advanced, null, "setlist-pdf"),
        new("sections.detect-again", Expert, null, "mixer-sections-detection"),
        new("sections.confidence", Expert, null, "mixer-sections-detection"),
        new("recording.input-mode", Advanced, null, "recording"),
        new("recording.monitoring-volume", Advanced, null, "recording-monitoring"),
        new("recording.manual-latency", Advanced, null, "recording-latency"),
        new("recording.loop", Advanced, null, "recording-loop-takes"),
        new("recording.take-pan", Advanced, null, "recording-compare"),
        new("recording.export-options", Advanced, null, "recording-export"),
        new("recording.latency-samples", Expert, null, "recording-latency"),
        new("recording.timing-adjust", Expert, null, "recording-timing"),
        new("recording.diagnostics", Expert, null, "recording"),
        new("chords.view", Beginner, null, "chords"),
        new("chords.full", Advanced, null, "chords-advanced"),
        new("chords.edit", Advanced, null, "chords-advanced"),
        new("chords.export", Advanced, null, "chords-advanced"),
        new("chords.engine", Expert, null, "chords-advanced"),
        new("scores.chords", Advanced, null, "chords-advanced"),
        new("tab.basic", Beginner, null, "tablature"),
        new("tab.tunings", Advanced, null, "tablature"),
        new("drums.trainer", Beginner, null, "drum-trainer"),
        new("drums.guide", Advanced, null, "drum-trainer"),
        new("singing.feedback", Beginner, null, "singing-feedback"),
        new("singing.tolerance", Advanced, null, "singing-tolerance-latency"),
        new("singing.raw", Expert, null, "singing-tolerance-latency"),
    }.ToDictionary(f => f.Id, StringComparer.Ordinal);

    /// <summary>Every entry in catalogue order.</summary>
    public static IReadOnlyList<UiFeature> All { get; } = [.. ById.Values];

    /// <summary>Gets an entry.</summary>
    /// <param name="id">Catalogue id, e.g. <c>mixer.pan</c>.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentException">The id is not in the catalogue.</exception>
    public static UiFeature Get(string id) =>
        ById.TryGetValue(id, out var feature) ? feature : throw new ArgumentException($"Unknown UI feature '{id}'.", nameof(id));

    /// <summary>True iff <c>Min &lt;= level &lt;= (Max ?? Expert)</c>.</summary>
    /// <param name="id">Catalogue id.</param>
    /// <param name="level">The level.</param>
    /// <returns>Whether the feature is shown at that level.</returns>
    /// <exception cref="ArgumentException">The id is not in the catalogue.</exception>
    public static bool IsVisible(string id, ExperienceLevel level)
    {
        var feature = Get(id);
        return feature.Min <= level && level <= (feature.Max ?? Expert);
    }
}
