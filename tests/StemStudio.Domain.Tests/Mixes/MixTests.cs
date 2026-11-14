using StemStudio.Domain.Mixes;
using StemStudio.Domain.Tests.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Mixes;

public sealed class MixTests
{
    private static readonly DateTime T0 = new(2026, 11, 14, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_StoresTrimmedNameAndSettings()
    {
        var track = CompletedTrack();
        var settings = Settings(" Guitar practice ") with { Loop = new LoopRegion(10, 20, true) };

        var mix = Mix.Create(Guid.NewGuid(), track, settings, T0);

        mix.TrackId.ShouldBe(track.Id);
        mix.Name.ShouldBe("Guitar practice");
        mix.NormalizedName.ShouldBe("GUITAR PRACTICE");
        mix.MasterVolume.ShouldBe(1.0);
        mix.Channels.Single().Stem.ShouldBe("guitar");
        mix.Metronome.ShouldBe(settings.Metronome);
        mix.Loop.ShouldBe(new LoopRegion(10, 20, true));
        mix.CreatedAt.ShouldBe(T0);
        mix.UpdatedAt.ShouldBe(T0);
        mix.Settings.Name.ShouldBe("Guitar practice");
        mix.Settings.Channels.ShouldBe(mix.Channels);
    }

    [Fact]
    public void Defaults_MatchTheDataModel()
    {
        var channel = ChannelSettings.Default("bass");
        var settings = new MixSettings { Name = "x", Metronome = MetronomeSettings.DefaultFor(null) };

        channel.Volume.ShouldBe(0.8);
        channel.Pan.ShouldBe(0);
        channel.Mute.ShouldBeFalse();
        channel.Solo.ShouldBeFalse();
        settings.MasterVolume.ShouldBe(1.0);
        settings.Channels.ShouldBeEmpty();
        settings.Loop.ShouldBeNull();
        settings.Metronome.ShouldBe(new MetronomeSettings { Enabled = false, Volume = 0.7, BpmOverride = null, OffsetMs = 0, BeatsPerBar = 4 });
    }

    [Fact]
    public void MetronomeDefault_FollowsBeatGrid()
    {
        var grid = TrackTests.ValidGrid() with { Reliable = true, BeatsPerBar = 3 };

        var metronome = MetronomeSettings.DefaultFor(grid);

        metronome.Enabled.ShouldBeTrue();
        metronome.BeatsPerBar.ShouldBe(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsEmptyName(string name)
    {
        FieldsOf(Settings(name)).ShouldBe(["name"]);
    }

    [Fact]
    public void Validate_AcceptsNameOf100AndRejects101()
    {
        FieldsOf(Settings(new string('n', 100))).ShouldBeEmpty();
        FieldsOf(Settings(new string('n', 101))).ShouldBe(["name"]);
        FieldsOf(Settings("  " + new string('n', 100) + "  ")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Validate_RejectsMasterVolumeOutOfRange(double volume)
    {
        FieldsOf(Settings() with { MasterVolume = volume }).ShouldBe(["masterVolume"]);
    }

    [Theory]
    [InlineData(-0.1, 0, "channels[0].volume")]
    [InlineData(1.1, 0, "channels[0].volume")]
    [InlineData(0.5, -1.1, "channels[0].pan")]
    [InlineData(0.5, 1.1, "channels[0].pan")]
    public void Validate_RejectsChannelValuesOutOfRange(double volume, double pan, string field)
    {
        var settings = Settings() with { Channels = [ChannelSettings.Default("drums") with { Volume = volume, Pan = pan }] };

        FieldsOf(settings).ShouldBe([field]);
    }

    [Fact]
    public void Validate_AcceptsChannelBoundaries()
    {
        var settings = Settings() with
        {
            Channels = [ChannelSettings.Default("drums") with { Volume = 0, Pan = -1 }, ChannelSettings.Default("bass") with { Volume = 1, Pan = 1 }],
        };

        FieldsOf(settings).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_RejectsDuplicateStemEntries()
    {
        var settings = Settings() with { Channels = [ChannelSettings.Default("drums"), ChannelSettings.Default("drums")] };

        FieldsOf(settings).ShouldBe(["channels[1].stem"]);
    }

    [Fact]
    public void Validate_RejectsStemNotOnTrack()
    {
        var fourStemTrack = CompletedTrack(TrackTests.Completion(model: "htdemucs", stems: StemCatalog.FourStem.Select(n => new Stem(n, 1)).ToList()));
        var settings = Settings() with { Channels = [ChannelSettings.Default("guitar")] };

        var errors = settings.Validate(fourStemTrack);

        errors.Single().Field.ShouldBe("channels[0].stem");
        errors.Single().Message.ShouldContain("guitar");
    }

    [Fact]
    public void Validate_RejectsTrackThatIsNotCompleted()
    {
        var queued = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", T0);

        Settings().Validate(queued).Select(e => e.Field).ShouldContain("trackId");
    }

    public static TheoryData<MetronomeCase> OutOfRangeMetronomes =>
    [
        new MetronomeCase(29.9, 0, 4, -0.1, "metronome.bpmOverride"),
        new MetronomeCase(300.1, 0, 4, 0.5, "metronome.bpmOverride"),
        new MetronomeCase(null, -2001, 4, 0.5, "metronome.offsetMs"),
        new MetronomeCase(null, 2001, 4, 0.5, "metronome.offsetMs"),
        new MetronomeCase(null, 0, 0, 0.5, "metronome.beatsPerBar"),
        new MetronomeCase(null, 0, 13, 0.5, "metronome.beatsPerBar"),
        new MetronomeCase(null, 0, 4, 1.5, "metronome.volume"),
    ];

    [Theory]
    [MemberData(nameof(OutOfRangeMetronomes))]
    public void Validate_RejectsMetronomeOutOfRange(MetronomeCase testCase)
    {
        FieldsOf(Settings() with { Metronome = testCase.ToSettings() }).ShouldContain(testCase.Field);
    }

    [Theory]
    [InlineData(30, -2000, 1)]
    [InlineData(300, 2000, 12)]
    public void Validate_AcceptsMetronomeBoundaries(double bpm, int offset, int beatsPerBar)
    {
        var metronome = new MetronomeSettings { Enabled = true, BpmOverride = bpm, OffsetMs = offset, BeatsPerBar = beatsPerBar };

        FieldsOf(Settings() with { Metronome = metronome }).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-1, 5, "loop.start")]
    [InlineData(10, 10, "loop.end")]
    [InlineData(10, 5, "loop.end")]
    [InlineData(10, 213.05, "loop.end")]
    public void Validate_RejectsInvalidLoop(double start, double end, string field)
    {
        FieldsOf(Settings() with { Loop = new LoopRegion(start, end, true) }).ShouldBe([field]);
    }

    [Fact]
    public void Validate_AcceptsLoopUpToTrackDuration()
    {
        FieldsOf(Settings() with { Loop = new LoopRegion(0, 213.04, false) }).ShouldBeEmpty();
    }

    [Fact]
    public void Create_ThrowsOnInvalidSettings()
    {
        var error = Should.Throw<DomainValidationException>(() => Mix.Create(Guid.NewGuid(), CompletedTrack(), Settings(""), T0));

        error.Errors.Single().Field.ShouldBe("name");
    }

    [Fact]
    public void Update_ReplacesSettingsAndTouchesUpdatedAt()
    {
        var track = CompletedTrack();
        var mix = Mix.Create(Guid.NewGuid(), track, Settings(), T0);

        mix.Update(track, Settings("Renamed") with { MasterVolume = 0.5, Channels = [] }, T0.AddMinutes(5));

        mix.Name.ShouldBe("Renamed");
        mix.NormalizedName.ShouldBe("RENAMED");
        mix.MasterVolume.ShouldBe(0.5);
        mix.Channels.ShouldBeEmpty();
        mix.CreatedAt.ShouldBe(T0);
        mix.UpdatedAt.ShouldBe(T0.AddMinutes(5));
    }

    [Fact]
    public void Update_ThrowsOnInvalidSettingsAndKeepsState()
    {
        var track = CompletedTrack();
        var mix = Mix.Create(Guid.NewGuid(), track, Settings(), T0);

        Should.Throw<DomainValidationException>(() => mix.Update(track, Settings() with { MasterVolume = 2 }, T0.AddMinutes(1)));

        mix.MasterVolume.ShouldBe(1.0);
        mix.UpdatedAt.ShouldBe(T0);
    }

    [Fact]
    public void Update_RejectsOtherTrack()
    {
        var mix = Mix.Create(Guid.NewGuid(), CompletedTrack(), Settings(), T0);

        Should.Throw<ArgumentException>(() => mix.Update(CompletedTrack(), Settings(), T0));
    }

    [Fact]
    public void Duplicate_CopiesSettingsUnderCopyName()
    {
        var track = CompletedTrack();
        var mix = Mix.Create(Guid.NewGuid(), track, Settings("Practice") with { Loop = new LoopRegion(1, 2, true) }, T0);
        var copyId = Guid.NewGuid();

        var copy = mix.Duplicate(copyId, ["PRACTICE", "PRACTICE (COPY)"], T0.AddHours(1));

        copy.Id.ShouldBe(copyId);
        copy.TrackId.ShouldBe(track.Id);
        copy.Name.ShouldBe("Practice (copy 2)");
        copy.Settings.ShouldBe(mix.Settings with { Name = "Practice (copy 2)" }, new MixSettingsComparer());
        copy.CreatedAt.ShouldBe(T0.AddHours(1));
    }

    [Fact]
    public void Naming_Normalizes()
    {
        MixNaming.Normalize("  Guitar Practice ").ShouldBe("GUITAR PRACTICE");
    }

    [Theory]
    [InlineData(new string[0], "Practice (copy)")]
    [InlineData(new[] { "PRACTICE (COPY)" }, "Practice (copy 2)")]
    [InlineData(new[] { "practice (copy)", "Practice (Copy 2)" }, "Practice (copy 3)")]
    public void Naming_FindsFirstFreeCopyName(string[] taken, string expected)
    {
        MixNaming.CopyName("Practice", taken).ShouldBe(expected);
    }

    [Fact]
    public void Naming_TruncatesBaseSoResultFitsIn100()
    {
        var name = new string('n', 100);

        var copy = MixNaming.CopyName(name, [MixNaming.Normalize(new string('n', 93) + " (copy)")]);

        copy.ShouldBe(new string('n', 91) + " (copy 2)");
        copy.Length.ShouldBe(100);
    }

    internal static Track CompletedTrack(TrackCompletion? completion = null)
    {
        var track = Track.FromUrl(Guid.NewGuid(), SourceKey.FromStored("youtube:dQw4w9WgXcQ"), "https://youtu.be/dQw4w9WgXcQ", T0);
        track.Complete(completion ?? TrackTests.Completion(), T0);
        return track;
    }

    internal static MixSettings Settings(string name = "Guitar practice") => new()
    {
        Name = name,
        MasterVolume = 1,
        Channels = [new ChannelSettings { Stem = "guitar", Volume = 0, Pan = 0, Mute = true, Solo = false }],
        Metronome = new MetronomeSettings { Enabled = true, Volume = 0.7, BpmOverride = null, OffsetMs = 0, BeatsPerBar = 4 },
        Loop = null,
    };

    private static List<string> FieldsOf(MixSettings settings) => settings.Validate(CompletedTrack()).Select(e => e.Field).ToList();

    private sealed class MixSettingsComparer : IEqualityComparer<MixSettings>
    {
        public bool Equals(MixSettings? x, MixSettings? y) =>
            x is not null && y is not null && x.Name == y.Name && x.MasterVolume.Equals(y.MasterVolume)
            && x.Channels.SequenceEqual(y.Channels) && x.Metronome == y.Metronome && x.Loop == y.Loop;

        public int GetHashCode(MixSettings obj) => obj.Name.GetHashCode(StringComparison.Ordinal);
    }
}

/// <summary>One out-of-range metronome setting and the field that must be reported.</summary>
public sealed record MetronomeCase(double? Bpm, int Offset, int BeatsPerBar, double Volume, string Field)
{
    /// <summary>The enabled metronome settings of this case.</summary>
    /// <returns>The settings.</returns>
    public MetronomeSettings ToSettings() => new() { Enabled = true, BpmOverride = Bpm, OffsetMs = Offset, BeatsPerBar = BeatsPerBar, Volume = Volume };
}
