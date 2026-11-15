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
            .ShouldBe(["mixer.pan", "mixer.loop.typed", "mixer.metronome.advanced", "mixer.numeric", "mixer.meter.db", "tracks.search",
                "mixes.search", "track.job-details", "shell.shortcut-overlay-key", "shell.kbd-hints", "artist.details", "artwork.own-picture", "export.format-details", "export.selected-channels", "library.filters", "export.range", "export.stem-pack",
                "karaoke.size", "karaoke.offset", "karaoke.timing-origin", "lyrics.align-device", "lyrics.refine"], ignoreOrder: true);
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
