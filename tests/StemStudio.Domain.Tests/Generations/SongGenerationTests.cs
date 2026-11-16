using StemStudio.Domain.Generations;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tests.Styles;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Generations;

/// <summary>010 T016: song captions, song and voice-conversion generations, vocal labels (FR-001, FR-008, FR-010, FR-011).</summary>
public sealed class SongGenerationTests
{
    private static readonly DateTime T0 = GenerationTestData.T0;
    private static readonly StyleTextGuard Guard = StyleTextGuard.Create([]);

    [Fact]
    public void Caption_StyleVector() =>
        SongCaptionBuilder.Build(new SongCaptionInput(StyleTestData.Preset().Snapshot(), null, LeadVoice.Female, "warm", false))
            .ShouldBe("Jazz swing, swing feel, piano, upright bass, brushed drums, relaxed, warm, female lead vocal, warm");

    [Fact]
    public void Caption_FreeCaptionVector() =>
        SongCaptionBuilder.Build(new SongCaptionInput(null, " pop rock, electric guitars, drums ", LeadVoice.Female, null, false))
            .ShouldBe("pop rock, electric guitars, drums, female lead vocal");

    [Theory]
    [InlineData(LeadVoice.Male, null, false, "pop, male lead vocal")]
    [InlineData(LeadVoice.Any, "breathy", false, "pop, lead vocal, breathy")]
    [InlineData(LeadVoice.Female, "soft", true, "pop, instrumental")]
    public void Caption_NeutralPopVoicesAndInstrumental(LeadVoice voice, string? delivery, bool instrumental, string expected) =>
        SongCaptionBuilder.Build(new SongCaptionInput(null, null, voice, delivery, instrumental)).ShouldBe(expected);

    [Fact]
    public void Caption_InstrumentalStyleSaysItOnce() =>
        SongCaptionBuilder.Build(new SongCaptionInput(StyleTestData.User().Snapshot(), null, LeadVoice.Any, null, true)).Split("instrumental").Length.ShouldBe(2);

    [Fact]
    public void Caption_IsAtMost512()
    {
        var caption = SongCaptionBuilder.Build(new SongCaptionInput(null, string.Join(' ', Enumerable.Repeat("word", 120)), LeadVoice.Female, "warm", false));

        caption.Length.ShouldBeLessThanOrEqualTo(512);
    }

    [Fact]
    public void Guard_ChecksFreeCaptionAndDelivery()
    {
        var errors = SongCaptionBuilder.Check(Guard, "Upbeat pop in the style of a chart singer", "sounds like a famous tenor");

        errors.Select(e => e.Field).ShouldBe(["freeCaption", "delivery"]);
        SongCaptionBuilder.Check(Guard, null, null).ShouldBeEmpty();
    }

    /// <summary>Song options with one invalid value and the field it names.</summary>
    public static TheoryData<SongOptions, string> InvalidOptions => new()
    {
        { new SongOptions(39, null, null, 60, 1, "x", null), "bpm" },
        { new SongOptions(null, "H major", null, 60, 1, "x", null), "keyScale" },
        { new SongOptions(null, null, "5", 60, 1, "x", null), "timeSignature" },
        { new SongOptions(null, null, null, 9, 1, "x", null), "durationSeconds" },
        { new SongOptions(null, null, null, 481, 1, "x", null), "durationSeconds" },
        { new SongOptions(null, null, null, 60, 3, "x", null), "takes" },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Options_AreValidated(SongOptions options, string field) =>
        SongRules.Validate(options).ShouldContain(e => e.Field == field);

    [Fact]
    public void Options_AcceptValidValuesAndLimitTexts()
    {
        SongRules.Validate(new SongOptions(96, "D minor", "4", 30, 2, null, null)).ShouldBeEmpty();
        SongRules.Validate(new SongOptions(96, "F# major", "6", 480, 1, null, "Bb")).ShouldBeEmpty();
        SongRules.Validate(new SongOptions(null, null, null, 60, 1, new string('d', 41), new string('c', 301))).Select(e => e.Field).ShouldBe(["delivery", "freeCaption"]);
    }

    [Fact]
    public void CreateSong_IsQueuedWithItsDetails()
    {
        var details = Song();

        var generation = Generation.CreateSong(Guid.NewGuid(), details, [42], T0);

        (generation.Kind, generation.Status, generation.Song, generation.Restyle, generation.SourceTrackId).ShouldBe((GenerationKind.Song, GenerationState.Queued, details, null, null));
    }

    [Fact]
    public void Retry_CopiesSongAndConversionDetails()
    {
        var song = Generation.CreateSong(Guid.NewGuid(), Song(), [42], T0);
        song.Cancel(T0);
        var conversion = Generation.CreateVoiceConversion(Guid.NewGuid(), Conversion(), T0);
        conversion.Cancel(T0);

        song.Retry(Guid.NewGuid(), [7], T0).Song.ShouldBe(song.Song);
        conversion.Retry(Guid.NewGuid(), [0], T0).VoiceConversion.ShouldBe(conversion.VoiceConversion);
    }

    [Fact]
    public void VoiceConversion_WaitsForStemsThenFollowsTheGenerator()
    {
        var generation = Generation.CreateVoiceConversion(Guid.NewGuid(), Conversion(), T0);

        generation.Seeds.ShouldBe([0]);
        generation.WaitForStems(Guid.NewGuid(), T0);
        (generation.Status, generation.Stage).ShouldBe((GenerationState.WaitingForStems, "Separating the take into stems"));
        generation.VoiceConversion!.ImportedTrackId.ShouldNotBeNull();
        generation.ApplyReport(new GenerationReport(GenerationState.Running, 10, "Converting", null, []), T0);
        generation.Status.ShouldBe(GenerationState.Running);
    }

    [Fact]
    public void DetachVoiceProfile_KeepsTheNameMarkedDeleted()
    {
        var details = Conversion();
        var generation = Generation.CreateVoiceConversion(Guid.NewGuid(), details, T0);
        generation.AssignJob(GenerationTestData.JobId, T0);
        generation.Complete([GenerationTestData.Take(1) with { Vocal = VocalLabel.Converted, VoiceProfileName = "My voice", VocalFilePath = "generations/x/vocal-1.flac" }], T0);

        generation.DetachVoiceProfile(Guid.NewGuid(), T0).ShouldBeFalse();
        generation.DetachVoiceProfile(details.VoiceProfileId!.Value, T0).ShouldBeTrue();

        generation.VoiceConversion!.VoiceProfileId.ShouldBeNull();
        generation.VoiceConversion.VoiceProfileName.ShouldBe("My voice (profile deleted)");
        generation.Takes.Single().VoiceProfileName.ShouldBe("My voice (profile deleted)");
        generation.Take(1)!.VocalFilePath.ShouldBe("generations/x/vocal-1.flac");
        Generation.CreateSong(Guid.NewGuid(), Song(), [1], T0).DetachVoiceProfile(Guid.NewGuid(), T0).ShouldBeFalse();
    }

    [Fact]
    public void TrackOrigin_CarriesTheVocalLabel()
    {
        var origin = new TrackOrigin(Guid.NewGuid(), 1, GenerationKind.Song, null) { Vocal = VocalLabel.Synthetic, LyricsSheetId = Guid.NewGuid() };

        (origin.Vocal, origin.VoiceProfileName).ShouldBe((VocalLabel.Synthetic, null));
    }

    internal static SongDetails Song() => new()
    {
        Lyrics = new LyricsSnapshot(Guid.NewGuid(), "This little song", "en", "[verse]\nla"),
        Style = null,
        FreeCaption = "pop rock",
        LeadVoice = LeadVoice.Female,
        Delivery = null,
        Instrumental = false,
        DurationSeconds = 60,
        Caption = "pop rock, female lead vocal",
    };

    internal static VoiceConversionDetails Conversion() => new()
    {
        SourceGenerationId = Guid.NewGuid(),
        SourceTakeIndex = 1,
        VoiceProfileId = Guid.NewGuid(),
        VoiceProfileName = "My voice",
        PitchShift = 0,
        AutoShift = true,
    };
}
