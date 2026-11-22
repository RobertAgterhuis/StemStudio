using StemStudio.Web.Experience;

namespace StemStudio.Web.Tests.Experience;

public sealed class FeatureCatalogTests
{
    private const ExperienceLevel B = ExperienceLevel.Beginner;
    private const ExperienceLevel A = ExperienceLevel.Advanced;
    private const ExperienceLevel E = ExperienceLevel.Expert;

    /// <summary>design-spec.md 5: id, visible levels, help topic, 1.2.0 test ids it gates.</summary>
    public static TheoryData<string, ExperienceLevel[], string> Catalogue => new()
    {
        { "mixer.pan", [A, E], "mixer-pan" },
        { "mixer.pan-marker", [B], "mixer-pan" },
        { "mixer.loop.typed", [A, E], "mixer-loop" },
        { "mixer.metronome.advanced", [A, E], "mixer-metronome" },
        { "mixer.numeric", [E], "mixer-numeric-entry" },
        { "mixer.meter.db", [E], "mixer-meters" },
        { "mixer.presets", [B, A, E], "mixer-presets" },
        { "practice.speed-reset", [A, E], "practice-speed" },
        { "practice.trainer", [A, E], "practice-speed-trainer" },
        { "practice.count-in.options", [A, E], "practice-count-in" },
        { "practice.numeric", [E], "practice-speed" },
        { "practice.stretch-status", [E], "practice-sound-quality" },
        { "tracks.search", [A, E], "tracks-library" },
        { "mixes.search", [A, E], "mixes-library" },
        { "track.job-details", [E], "track-job-details" },
        { "shell.shortcut-overlay-key", [E], "shortcuts" },
        { "shell.kbd-hints", [E], "shortcuts" },
        { "journey.rail", [B], "journey" },
        { "help.inline-hints", [B], "levels" },
        { "help.tour-offers", [B, A, E], "tours" },
        { "artist.details", [A, E], "band-info" },
        { "artwork.own-picture", [A, E], "artwork" },
        { "export.format-details", [A, E], "export-formats" },
        { "export.selected-channels", [A, E], "export-dialog" },
        { "library.filters", [A, E], "library" },
        { "export.range", [A, E], "export-range" },
        { "export.stem-pack", [A, E], "export-dialog" },
        { "karaoke.size", [A, E], "karaoke" },
        { "karaoke.offset", [A, E], "karaoke-offset" },
        { "karaoke.timing-origin", [E], "karaoke" },
        { "lyrics.align-device", [A, E], "lyrics-align" },
        { "lyrics.refine", [E], "lyrics-align" },
        { "lyrics.remove", [A, E], "lyrics-sources" },
        { "score.detail-stems", [A, E], "sheet-music" },
        { "score.key-override", [A, E], "sheet-music-key-tempo" },
        { "score.manual-tempo", [A, E], "sheet-music-key-tempo" },
        { "score.quality", [A, E], "sheet-music-piano" },
        { "score.range", [A, E], "sheet-music" },
        { "restyle.options", [A, E], "restyle-options" },
        { "song.options", [A, E], "song-options" },
        { "voice.pitch", [A, E], "voice-conversion" },
        { "agent.steps", [A, E], "agent-panel" },
        { "agent.steps-compact", [B], "agent-panel" },
        { "agent.conversation-actions", [A, E], "agent-panel" },
        { "agent.usage", [A, E], "agent-backends" },
        { "agent.tool-log", [E], "agent-panel" },
        { "agent.models", [A, E], "agent-backends" },
        { "agent.claude-options", [E], "agent-backends" },
        { "care.backup-options", [A, E], "backup-contents" },
        { "care.job-bytes", [A, E], "library-care" },
        { "care.verify", [A, E], "backup-compatibility" },
        { "care.manifest", [E], "backup-contents" },
        { "care.track-table", [A, E], "storage-overview" },
        { "care.top-tracks", [B], "storage-overview" },
        { "care.worker-entries", [E], "storage-regenerable" },
        { "care.restore-merge", [A, E], "backup-restore" },
        { "lyrics.assist-count", [A, E], "lyric-assist" },
        { "lyrics.assist-titles", [A, E], "lyric-assist" },
        { "lyrics.assist-filters", [A, E], "lyric-assist" },
        { "lyrics.assist-layers", [A, E], "lyric-assist-privacy" },
        { "lyrics.assist-usage", [A, E], "lyric-assist-privacy" },
        { "lyrics.assist-sensitivity", [A, E], "lyric-assist-licence" },
        { "lyrics.assist-marks", [A, E], "lyric-assist-licence" },
        { "lyrics.assist-rhythm", [A, E], "lyric-assist-rhythm" },
        { "lyrics.assist-source", [E], "lyric-assist-privacy" },
        { "lyrics.assist-log", [E], "lyric-assist-privacy" },
        { "lyrics.assist-revisions", [E], "lyric-assist" },
        { "shortcuts.settings", [A, E], "shortcuts-custom" },
        { "sections.edit", [A, E], "mixer-sections-edit" },
        { "setlists.notes", [A, E], "setlists" },
        { "practice.log-sections", [A, E], "mixer-practice-log" },
        { "practice.log-tempo", [A, E], "mixer-practice-log" },
        { "practice.sessions", [E], "practice-privacy" },
        { "setlists.pdf-options", [A, E], "setlist-pdf" },
        { "sections.detect-again", [E], "mixer-sections-detection" },
        { "sections.confidence", [E], "mixer-sections-detection" },
        { "recording.input-mode", [A, E], "recording" },
        { "recording.monitoring-volume", [A, E], "recording-monitoring" },
        { "recording.manual-latency", [A, E], "recording-latency" },
        { "recording.loop", [A, E], "recording-loop-takes" },
        { "recording.take-pan", [A, E], "recording-compare" },
        { "recording.export-options", [A, E], "recording-export" },
        { "recording.latency-samples", [E], "recording-latency" },
        { "recording.timing-adjust", [E], "recording-timing" },
        { "recording.diagnostics", [E], "recording" },
        { "chords.view", [B, A, E], "chords" },
        { "chords.full", [A, E], "chords-advanced" },
        { "chords.edit", [A, E], "chords-advanced" },
        { "chords.export", [A, E], "chords-advanced" },
        { "chords.engine", [E], "chords-advanced" },
        { "scores.chords", [A, E], "chords-advanced" },
        { "tab.basic", [B, A, E], "tablature" },
        { "tab.tunings", [A, E], "tablature" },
        { "drums.trainer", [B, A, E], "drum-trainer" },
        { "drums.guide", [A, E], "drum-trainer" },
    };

    private static readonly Dictionary<string, string[]> GatedReleasedIds = new(StringComparer.Ordinal)
    {
        ["mixer.pan"] = ["pan-<stem>", "pan-<parent>-<detail>"],
        ["mixer.loop.typed"] = ["loop-start", "loop-end"],
        ["mixer.metronome.advanced"] = ["metronome-bpm", "metronome-offset", "metronome-beats-per-bar", "metronome-reset"],
    };

    [Theory]
    [MemberData(nameof(Catalogue))]
    public void EveryEntry_IsVisibleExactlyAtItsLevels(string id, ExperienceLevel[] levels, string topic)
    {
        var feature = FeatureCatalog.Get(id);

        feature.TopicId.ShouldBe(topic);
        foreach (var level in Enum.GetValues<ExperienceLevel>())
        {
            FeatureCatalog.IsVisible(id, level).ShouldBe(levels.Contains(level), $"{id} at {level}");
        }
    }

    [Fact]
    public void Catalogue_HasExactlyTheDocumentedEntries_WithValidIds()
    {
        FeatureCatalog.All.Select(f => f.Id).Order(StringComparer.Ordinal)
            .ShouldBe(Catalogue.Select(row => row.Data.Item1).Order(StringComparer.Ordinal));
        FeatureCatalog.All.ShouldAllBe(f => System.Text.RegularExpressions.Regex.IsMatch(f.Id, @"^[a-z]+(\.[a-z0-9-]+)+$"));
    }

    [Fact]
    public void Expert_SeesEveryControlAdvancedSees()
    {
        var controls = FeatureCatalog.All.Where(f => f.Max is null);

        controls.Where(f => FeatureCatalog.IsVisible(f.Id, A)).ShouldAllBe(f => FeatureCatalog.IsVisible(f.Id, E));
    }

    [Fact]
    public void HiddenAtBeginner_AmongReleasedIds_EqualsRuleR8()
    {
        var hidden = FeatureCatalog.All
            .Where(f => !FeatureCatalog.IsVisible(f.Id, B) && GatedReleasedIds.ContainsKey(f.Id))
            .SelectMany(f => GatedReleasedIds[f.Id])
            .Order(StringComparer.Ordinal);

        hidden.ShouldBe(new[]
        {
            "loop-end", "loop-start", "metronome-beats-per-bar", "metronome-bpm", "metronome-offset", "metronome-reset",
            "pan-<parent>-<detail>", "pan-<stem>",
        }.Order(StringComparer.Ordinal));
        FeatureCatalog.All.Where(f => !FeatureCatalog.IsVisible(f.Id, B) && f.Max is null)
            .Select(f => f.Id)
            .ShouldBe(["mixer.pan", "mixer.loop.typed", "mixer.metronome.advanced", "mixer.numeric", "mixer.meter.db", "practice.speed-reset", "practice.trainer", "practice.count-in.options", "practice.numeric", "practice.stretch-status", "tracks.search",
                "mixes.search", "track.job-details", "shell.shortcut-overlay-key", "shell.kbd-hints", "artist.details", "artwork.own-picture", "export.format-details", "export.selected-channels", "library.filters", "export.range", "export.stem-pack",
                "karaoke.size", "karaoke.offset", "karaoke.timing-origin", "lyrics.align-device", "lyrics.refine", "lyrics.remove",
                "score.detail-stems", "score.key-override", "score.manual-tempo", "score.quality", "score.range",
                "restyle.options", "song.options", "voice.pitch", "agent.steps", "agent.conversation-actions", "agent.usage", "agent.tool-log", "agent.models", "agent.claude-options",
                "care.backup-options", "care.job-bytes", "care.verify", "care.manifest",
                "care.track-table", "care.worker-entries", "care.restore-merge",
                "lyrics.assist-count", "lyrics.assist-titles", "lyrics.assist-filters", "lyrics.assist-layers", "lyrics.assist-usage", "lyrics.assist-sensitivity",
                "lyrics.assist-marks", "lyrics.assist-rhythm", "lyrics.assist-source", "lyrics.assist-log", "lyrics.assist-revisions", "shortcuts.settings", "sections.edit", "sections.detect-again", "sections.confidence", "setlists.notes", "setlists.pdf-options", "practice.log-sections", "practice.log-tempo", "practice.sessions",
                "recording.input-mode", "recording.monitoring-volume", "recording.manual-latency", "recording.loop", "recording.take-pan", "recording.export-options", "recording.latency-samples", "recording.timing-adjust", "recording.diagnostics",
                "chords.full", "chords.edit", "chords.export", "chords.engine", "scores.chords", "tab.tunings", "drums.guide"], ignoreOrder: true);
    }

    [Fact]
    public void UnknownId_Throws()
    {
        Should.Throw<ArgumentException>(() => FeatureCatalog.Get("mixer.nope")).Message.ShouldContain("mixer.nope");
        Should.Throw<ArgumentException>(() => FeatureCatalog.IsVisible("mixer.nope", A));
    }

    [Fact]
    public void Context_DelegatesToTheCatalogue()
    {
        var context = new ExperienceContext(B, StemStudio.Web.Preferences.DensityPreference.Comfortable);

        context.IsVisible("journey.rail").ShouldBeTrue();
        context.IsVisible("mixer.pan").ShouldBeFalse();
        context.Is(B).ShouldBeTrue();
        context.Is(E).ShouldBeFalse();
        ExperienceContext.Default.Level.ShouldBe(A);
    }
}
