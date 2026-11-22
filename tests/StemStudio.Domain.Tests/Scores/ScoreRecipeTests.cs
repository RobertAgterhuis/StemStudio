using StemStudio.Domain.Assets;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Scores;

/// <summary>Feature 008 T030: profiles, request defaults and limits, score facts and score items (data-model 2.1, 2.7, 4.1, 4.2; FR-002, FR-006, FR-008, FR-009).</summary>
public sealed class ScoreRecipeTests
{
    private static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
    private static readonly DateTime T0 = new(2026, 11, 15, 14, 0, 0, DateTimeKind.Utc);
    private static readonly ScoreContext Cpu = new(262.0, DrumsSplit: false, GpuWorker: false);

    [Theory]
    [InlineData("vocals", ScoreProfile.Vocals)]
    [InlineData("vocals.lead", ScoreProfile.Vocals)]
    [InlineData("bass", ScoreProfile.Bass)]
    [InlineData("guitar", ScoreProfile.Guitar)]
    [InlineData("guitar.rhythm", ScoreProfile.Guitar)]
    [InlineData("piano", ScoreProfile.Piano)]
    [InlineData("piano.rest", ScoreProfile.Keys)]
    [InlineData("other", ScoreProfile.Keys)]
    [InlineData("other.rest", ScoreProfile.Keys)]
    [InlineData("drums", ScoreProfile.Drums)]
    public void Suggest_FollowsTheChannel(string channel, ScoreProfile profile)
    {
        ProfileCatalog.Suggest(channel).ShouldBe(profile);
    }

    [Theory]
    [InlineData("drums.kick")]
    [InlineData("unknown")]
    public void Suggest_SingleDrumPiecesAndUnknownChannelsHaveNone(string channel) => ProfileCatalog.Suggest(channel).ShouldBeNull();

    [Theory]
    [InlineData("drums.kick")]
    [InlineData("drums.snare")]
    [InlineData("drums.toms")]
    [InlineData("drums.hihat")]
    [InlineData("drums.cymbals")]
    public void Allowed_ASingleDrumPieceOffersTheDrumsProfile(string channel) => ProfileCatalog.Allowed(channel).ShouldBe([ScoreProfile.Drums]);

    [Theory]
    [InlineData("unknown")]
    [InlineData("drums.rest")]
    [InlineData("vocals.lead.x")]
    public void Allowed_UnknownChannelsOfferNothing(string channel) => ProfileCatalog.Allowed(channel).ShouldBeEmpty();

    [Fact]
    public void ASingleDrumPiece_NeedsTheSplitAndTheDrumsProfile()
    {
        var kick = new ScoreRequest(ScoreSource.FromChannel(TrackId, "drums.kick")) { Profile = ScoreProfile.Drums };

        Should.Throw<ScoreConflictException>(() => ScoreRecipeRules.Resolve(kick, Cpu));
        ScoreRecipeRules.Resolve(kick, Cpu with { DrumsSplit = true }).Profile.ShouldBe(ScoreProfile.Drums);
        Should.Throw<DomainValidationException>(() => ScoreRecipeRules.Resolve(kick with { Profile = ScoreProfile.Bass }, Cpu));
    }

    private static ScoreRequest Tab(ScoreProfile profile, TuningId tuning, int capo, TabLayout layout = TabLayout.NotationAndTab) =>
        new(ScoreSource.FromChannel(TrackId, profile == ScoreProfile.Bass ? "bass" : "guitar")) { Profile = profile, Tablature = new TablatureSettings(tuning, capo, layout) };

    [Fact]
    public void Tablature_IsKeptInTheRecipeForGuitarAndBass()
    {
        ScoreRecipeRules.Resolve(Tab(ScoreProfile.Guitar, TuningId.GuitarDropD, 2, TabLayout.TabOnly), Cpu).Tablature.ShouldBe(new TablatureSettings(TuningId.GuitarDropD, 2, TabLayout.TabOnly));
        ScoreRecipeRules.Resolve(Tab(ScoreProfile.Bass, TuningId.Bass5Standard, 0), Cpu).Tablature.ShouldBe(new TablatureSettings(TuningId.Bass5Standard, 0, TabLayout.NotationAndTab));
        ScoreRecipeRules.Resolve(new ScoreRequest(ScoreSource.FromChannel(TrackId, "bass")), Cpu).Tablature.ShouldBeNull();
    }

    [Theory]
    [InlineData(ScoreProfile.Vocals)]
    [InlineData(ScoreProfile.Keys)]
    [InlineData(ScoreProfile.Piano)]
    public void Tablature_ForOtherProfiles_IsNotAllowed(ScoreProfile profile) =>
        Should.Throw<ScoreUnprocessableException>(() => ScoreRecipeRules.Resolve(Tab(profile, TuningId.GuitarStandard, 0), Cpu)).Code.ShouldBe(ScoreUnprocessable.TablatureNotAllowed);

    [Fact]
    public void Tablature_WithAnotherInstrumentsTuning_IsAMismatch()
    {
        Should.Throw<ScoreUnprocessableException>(() => ScoreRecipeRules.Resolve(Tab(ScoreProfile.Guitar, TuningId.Bass4Standard, 0), Cpu)).Code.ShouldBe(ScoreUnprocessable.TuningMismatch);
        Should.Throw<ScoreUnprocessableException>(() => ScoreRecipeRules.Resolve(Tab(ScoreProfile.Bass, TuningId.GuitarStandard, 0), Cpu)).Code.ShouldBe(ScoreUnprocessable.TuningMismatch);
    }

    [Fact]
    public void Tablature_ACapoForABass_IsNotAllowed() =>
        Should.Throw<ScoreUnprocessableException>(() => ScoreRecipeRules.Resolve(Tab(ScoreProfile.Bass, TuningId.Bass4Standard, 3), Cpu)).Code.ShouldBe(ScoreUnprocessable.CapoNotAllowed);

    [Theory]
    [InlineData(-1)]
    [InlineData(13)]
    public void Tablature_CapoOutsideZeroToTwelve_IsAFieldError(int capo) =>
        Should.Throw<DomainValidationException>(() => ScoreRecipeRules.Resolve(Tab(ScoreProfile.Guitar, TuningId.GuitarStandard, capo), Cpu)).Errors.Single().Field.ShouldBe("tablature.capo");

    [Fact]
    public void Tablature_FollowsTheCheckOrder_ProfileThenTuningThenCapo()
    {
        Should.Throw<ScoreUnprocessableException>(() => ScoreRecipeRules.Resolve(Tab(ScoreProfile.Keys, TuningId.Bass4Standard, 99), Cpu)).Code.ShouldBe(ScoreUnprocessable.TablatureNotAllowed);
        Should.Throw<ScoreUnprocessableException>(() => ScoreRecipeRules.Resolve(Tab(ScoreProfile.Bass, TuningId.GuitarStandard, 3), Cpu)).Code.ShouldBe(ScoreUnprocessable.TuningMismatch);
    }

    [Fact]
    public void Allowed_PitchedSourcesOfferEveryPitchedProfileAndDrumsOnlyDrums()
    {
        ProfileCatalog.Allowed("bass").ShouldBe([ScoreProfile.Vocals, ScoreProfile.Bass, ScoreProfile.Guitar, ScoreProfile.Keys, ScoreProfile.Piano]);
        ProfileCatalog.Allowed("drums").ShouldBe([ScoreProfile.Drums]);
        ProfileCatalog.Allowed(null).ShouldBe(ProfileCatalog.Pitched);
    }

    [Theory]
    [InlineData(ScoreProfile.Vocals, ScoreResolution.Eighth, "Vocals")]
    [InlineData(ScoreProfile.Bass, ScoreResolution.Sixteenth, "Bass")]
    [InlineData(ScoreProfile.Drums, ScoreResolution.Sixteenth, "Drums")]
    public void ProfileFacts_ResolutionNameAndLimit(ScoreProfile profile, ScoreResolution resolution, string name)
    {
        (ProfileCatalog.DefaultResolution(profile), ProfileCatalog.InstrumentName(profile)).ShouldBe((resolution, name));
        ProfileCatalog.Limit(profile).Length.ShouldBeInRange(1, 500);
    }

    [Theory]
    [InlineData(ScoreProfile.Piano, ScoreQuality.Accurate, ScoreEngine.PianoCrnn)]
    [InlineData(ScoreProfile.Piano, ScoreQuality.Fast, ScoreEngine.BasicPitch)]
    [InlineData(ScoreProfile.Drums, null, ScoreEngine.DrumOnsets)]
    [InlineData(ScoreProfile.Bass, null, ScoreEngine.BasicPitch)]
    public void Engine_DependsOnProfileAndQuality(ScoreProfile profile, ScoreQuality? quality, ScoreEngine engine)
    {
        ProfileCatalog.Engine(profile, quality).ShouldBe(engine);
    }

    [Fact]
    public void KeyNames_AreTheThirtyMusic21KeySignatures()
    {
        KeyNames.All.Count.ShouldBe(30);
        KeyNames.All.ShouldContain("B- major");
        KeyNames.All.ShouldContain("A# minor");
        (KeyNames.IsKnown("E minor"), KeyNames.IsKnown("e minor"), KeyNames.IsKnown("H major")).ShouldBe((true, false, false));
    }

    [Fact]
    public void Resolve_FillsTheDefaultsOfAChannel()
    {
        var recipe = ScoreRecipeRules.Resolve(new ScoreRequest(ScoreSource.FromChannel(TrackId, "bass")), Cpu);

        (recipe.Profile, recipe.Quality, recipe.Resolution, recipe.KeyOverride, recipe.Tempo, recipe.Range).ShouldBe((ScoreProfile.Bass, null, ScoreResolution.Sixteenth, null, null, null));
        recipe.Source.ShouldBe(ScoreSource.FromChannel(TrackId, "bass"));
    }

    [Theory]
    [InlineData(false, ScoreQuality.Fast)]
    [InlineData(true, ScoreQuality.Accurate)]
    public void Resolve_PianoQualityDefaultsByDevice(bool gpu, ScoreQuality quality)
    {
        var recipe = ScoreRecipeRules.Resolve(new ScoreRequest(ScoreSource.FromChannel(TrackId, "piano")), Cpu with { GpuWorker = gpu });

        recipe.Quality.ShouldBe(quality);
    }

    [Fact]
    public void Resolve_KeepsExplicitValuesAndDropsQualityForOtherProfiles()
    {
        var request = new ScoreRequest(ScoreSource.FromAsset(Guid.NewGuid()))
        {
            Profile = ScoreProfile.Guitar,
            Quality = ScoreQuality.Accurate,
            Resolution = ScoreResolution.Eighth,
            KeyOverride = "E- major",
            Tempo = new ManualTempo(96, 3, 0.4),
            Range = new ExportRange(30, 75),
        };

        var recipe = ScoreRecipeRules.Resolve(request, Cpu);

        (recipe.Profile, recipe.Quality, recipe.Resolution, recipe.KeyOverride, recipe.Tempo, recipe.Range)
            .ShouldBe((ScoreProfile.Guitar, null, ScoreResolution.Eighth, "E- major", new ManualTempo(96, 3, 0.4), new ExportRange(30, 75)));
    }

    [Fact]
    public void Resolve_DrumsNeedTheSplitDrumsChannel()
    {
        var drums = new ScoreRequest(ScoreSource.FromChannel(TrackId, "drums"));

        Should.Throw<ScoreConflictException>(() => ScoreRecipeRules.Resolve(drums, Cpu)).Conflict.ShouldBe(ScoreConflict.DrumsNotSplit);
        ScoreRecipeRules.Resolve(drums, Cpu with { DrumsSplit = true }).Profile.ShouldBe(ScoreProfile.Drums);
    }

    [Theory]
    [InlineData("bass", ScoreProfile.Drums)]
    [InlineData("drums", ScoreProfile.Bass)]
    [InlineData("drums.kick", ScoreProfile.Bass)]
    public void Resolve_RejectsProfilesTheSourceDoesNotAllow(string channel, ScoreProfile profile)
    {
        var request = new ScoreRequest(ScoreSource.FromChannel(TrackId, channel)) { Profile = profile };

        Fields(request, Cpu with { DrumsSplit = true }).ShouldBe(["profile"]);
    }

    [Fact]
    public void Resolve_AnAssetHasNoDrumsAndNoSuggestionWithoutAProfile()
    {
        var asset = ScoreSource.FromAsset(Guid.NewGuid());

        Fields(new ScoreRequest(asset) { Profile = ScoreProfile.Drums }, Cpu).ShouldBe(["profile"]);
        Fields(new ScoreRequest(asset), Cpu).ShouldBe(["profile"]);
        ScoreRecipeRules.Resolve(new ScoreRequest(asset) { Profile = ScoreProfile.Piano }, Cpu).Quality.ShouldBe(ScoreQuality.Fast);
    }

    [Fact]
    public void Resolve_AnAssetUsesItsSuggestedProfile()
    {
        var recipe = ScoreRecipeRules.Resolve(new ScoreRequest(ScoreSource.FromAsset(Guid.NewGuid())), Cpu with { SuggestedProfile = ScoreProfile.Bass });

        recipe.Profile.ShouldBe(ScoreProfile.Bass);
    }

    [Theory]
    [InlineData(29.9, 4, 0, "tempo.bpm")]
    [InlineData(300.1, 4, 0, "tempo.bpm")]
    [InlineData(120, 0, 0, "tempo.beatsPerBar")]
    [InlineData(120, 13, 0, "tempo.beatsPerBar")]
    [InlineData(120, 4, -0.1, "tempo.firstDownbeatSeconds")]
    [InlineData(120, 4, 900.1, "tempo.firstDownbeatSeconds")]
    public void Resolve_RejectsTempoOutsideItsRanges(double bpm, int beatsPerBar, double firstDownbeat, string field)
    {
        var request = new ScoreRequest(ScoreSource.FromChannel(TrackId, "bass")) { Tempo = new ManualTempo(bpm, beatsPerBar, firstDownbeat) };

        Fields(request, Cpu).ShouldBe([field]);
    }

    [Theory]
    [InlineData(0, 1.9)]
    [InlineData(-1, 10)]
    [InlineData(10, 263)]
    [InlineData(20, 10)]
    public void Resolve_RejectsShortOrOutsideRanges(double start, double end)
    {
        var request = new ScoreRequest(ScoreSource.FromChannel(TrackId, "bass")) { Range = new ExportRange(start, end) };

        Fields(request, Cpu).ShouldBe(["range"]);
    }

    [Fact]
    public void Resolve_RejectsUnknownKeys()
    {
        var request = new ScoreRequest(ScoreSource.FromChannel(TrackId, "bass")) { KeyOverride = "H major" };

        Fields(request, Cpu).ShouldBe(["keyOverride"]);
    }

    [Fact]
    public void Texts_NameTheChannelAndTheSheetMusic()
    {
        (ScoreRecipeRules.NameSuffix("bass"), ScoreRecipeRules.Contents("bass")).ShouldBe(("bass (sheet music)", "bass, sheet music"));
    }

    [Fact]
    public void ScoreConflictException_CarriesTheConflict()
    {
        new ScoreConflictException(ScoreConflict.DrumsNotSplit).Conflict.ShouldBe(ScoreConflict.DrumsNotSplit);
        new ScoreConflictException().Conflict.ShouldBe(ScoreConflict.DrumsNotSplit);
        new ScoreConflictException("x").Message.ShouldBe("x");
        new ScoreConflictException("x", new InvalidOperationException()).InnerException.ShouldNotBeNull();
    }

    [Fact]
    public void ScoreUnprocessableException_CarriesTheCode()
    {
        new ScoreUnprocessableException(ScoreUnprocessable.CapoNotAllowed, "d").ShouldSatisfyAllConditions(e => e.Code.ShouldBe(ScoreUnprocessable.CapoNotAllowed), e => e.Message.ShouldBe("d"));
        new ScoreUnprocessableException().Code.ShouldBe(ScoreUnprocessable.TablatureNotAllowed);
        new ScoreUnprocessableException("x").Message.ShouldBe("x");
        new ScoreUnprocessableException("x", new InvalidOperationException()).InnerException.ShouldNotBeNull();
    }

    [Fact]
    public void ScoreFacts_PassTheirRules()
    {
        Should.NotThrow(() => ScoreFactsRules.Validate(Facts()));
    }

    [Fact]
    public void ScoreFacts_RejectBrokenWorkerValues()
    {
        var facts = Facts() with
        {
            Bpm = 20,
            TimeSignature = "13/4",
            Bars = 0,
            Notes = 0,
            Pages = 0,
            Key = new KeyInfo("H", KeyMode.Major, 1.5, KeySource.Estimated),
            Warnings = Enumerable.Repeat(new ScoreWarning(ScoreWarningCode.FewNotes, "Very few notes were detected."), 9).ToList(),
            Attribution = new string('x', 301),
        };

        var fields = Should.Throw<DomainValidationException>(() => ScoreFactsRules.Validate(facts)).Errors.Select(error => error.Field);

        fields.ShouldBe(["bpm", "timeSignature", "bars", "notes", "pages", "key.tonic", "key.confidence", "warnings", "attribution"], ignoreOrder: true);
    }

    [Fact]
    public void ScoreFacts_RejectEmptyWarningTexts()
    {
        var facts = Facts() with { Warnings = [new ScoreWarning(ScoreWarningCode.FewNotes, " ")] };

        Should.Throw<DomainValidationException>(() => ScoreFactsRules.Validate(facts)).Errors.Single().Field.ShouldBe("warnings[0].text");
    }

    [Fact]
    public void ScoreItems_AreQueuedThenReadyWithTheirFactsAndFiles()
    {
        var asset = Asset.CreateScore(Guid.NewGuid(), "Song - bass (sheet music)", ScoreProvenance(), T0);
        asset.AssignJob("c4e6a8b0d2f4416385a7c9e1b3d5f7a9", T0);
        var files = new[] { new AssetFile("pdf", "score.pdf", "application/pdf", 10, new string('a', 64)), new AssetFile("midi", "score.mid", "audio/midi", 5, new string('b', 64)) };

        var facts = Facts();

        asset.CompleteScore(files, facts, T0.AddMinutes(1));

        (asset.Kind, asset.Status, asset.SizeBytes, asset.Audio).ShouldBe((AssetKind.Score, AssetStatus.Ready, 15L, null));
        asset.Score.ShouldBeSameAs(facts);
        (asset.Provenance.Recipe, asset.Provenance.ScoreRecipe!.Profile, asset.TrackId).ShouldBe((null, ScoreProfile.Bass, TrackId));
    }

    [Fact]
    public void ScoreItems_NeedAScoreRecipeAndAudioItemsAnExportRecipe()
    {
        Should.Throw<ArgumentException>(() => Asset.CreateScore(Guid.NewGuid(), "x", ScoreProvenance() with { ScoreRecipe = null }, T0));
        Should.Throw<ArgumentException>(() => Asset.Create(Guid.NewGuid(), "x", ScoreProvenance(), T0));
        var audio = Asset.Create(Guid.NewGuid(), "x", ScoreProvenance() with { Recipe = new ExportRecipe { TrackId = TrackId, Mode = ExportMode.Raw, Format = new ExportFormat(AudioFormat.Flac, 16, null) } }, T0);
        Should.Throw<InvalidOperationException>(() => audio.CompleteScore([new AssetFile("pdf", "score.pdf", "application/pdf", 1, new string('a', 64))], Facts(), T0));
        Should.Throw<ArgumentException>(() => Asset.CreateScore(Guid.NewGuid(), "x", ScoreProvenance(), T0).CompleteScore([], Facts(), T0));
    }

    private static Provenance ScoreProvenance() => new()
    {
        Origin = AssetOrigin.Transcription,
        TrackId = TrackId,
        TrackTitle = "Never Gonna Give You Up",
        Contents = "bass, sheet music",
        ScoreRecipe = ScoreRecipeRules.Resolve(new ScoreRequest(ScoreSource.FromChannel(TrackId, "bass")), Cpu),
    };

    private static ScoreFacts Facts() => new()
    {
        Profile = ScoreProfile.Bass,
        Engine = ScoreEngine.BasicPitch,
        Key = new KeyInfo("A-", KeyMode.Major, 0.83, KeySource.Estimated),
        Bpm = 118,
        TimeSignature = "4/4",
        Bars = 125,
        Notes = 386,
        Pages = 3,
        Resolution = ScoreResolution.Sixteenth,
        Warnings = [new ScoreWarning(ScoreWarningCode.NotesCleaned, "87 very short or out-of-range notes were removed.")],
        Device = new JobDevice(ProcessingDeviceKind.Cpu, null, false, null),
    };

    private static string[] Fields(ScoreRequest request, ScoreContext context) =>
        Should.Throw<DomainValidationException>(() => ScoreRecipeRules.Resolve(request, context)).Errors.Select(error => error.Field).ToArray();
}
