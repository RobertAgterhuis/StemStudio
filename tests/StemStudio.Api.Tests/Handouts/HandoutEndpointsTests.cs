using System.Net.Http.Json;
using System.Net;
using StemStudio.Api.Handouts;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>
/// Feature 020 T062 and T064: options, the assembled document (facts, sections, lyrics with chords, notes, notice,
/// attributions, naming, <c>generatedOn</c>), creation (202, 404, 409, 422, 503), the asset sync of handout jobs filling
/// <c>Asset.handout</c>, "Make again" with the recipe, retry, and items that outlive their track (FR-008..FR-014).
/// </summary>
public sealed class HandoutEndpointsTests : ApiTestBase
{
    private static CreateHandoutRequest Bass(Guid trackId) => new() { Source = HandoutSourceDto.Song(trackId), Family = HandoutFamilyDto.Bass };

    [Fact]
    public async Task Options_ListTheIngredientsAndNotationChoices_AndMatchTheContract()
    {
        var track = await Factory.SeedSongAsync();
        var (score, tab) = (HandoutTestData.Score(track.Id, ("bass", ScoreProfile.Bass), 1), HandoutTestData.Score(track.Id, ("bass", ScoreProfile.Bass), 2, HandoutTestData.Tab));
        await Factory.SeedAssetsAsync(score, tab, HandoutTestData.Score(track.Id, ("guitar", ScoreProfile.Guitar), 3));

        using var response = await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Song(track.Id), HandoutFamilyDto.Bass), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getHandoutOptions");
        var options = await response.BodyAsync<HandoutOptionsDto>();
        (options.Kind, options.FamilyLabel, options.Songs, options.DefaultPaper, options.EstimateSeconds).ShouldBe((HandoutKindDto.Song, "Bass", 1, PaperDto.A4, 6));
        options.Ingredients.Select(item => item.Code).ShouldBe([IngredientCodeDto.Facts, IngredientCodeDto.FormChart, IngredientCodeDto.ChordsOverLyrics, IngredientCodeDto.Lyrics, IngredientCodeDto.ChordChart, IngredientCodeDto.Notation, IngredientCodeDto.PracticeNotes, IngredientCodeDto.PracticeProgress]);
        var byCode = options.Ingredients.ToDictionary(item => item.Code);
        (byCode[IngredientCodeDto.FormChart].Status, byCode[IngredientCodeDto.FormChart].Text).ShouldBe((IngredientStatusDto.Available, "5 sections"));
        (byCode[IngredientCodeDto.PracticeNotes].Status, byCode[IngredientCodeDto.PracticeNotes].Action).ShouldBe((IngredientStatusDto.Missing, (IngredientActionDto?)IngredientActionDto.WriteNotes));
        (byCode[IngredientCodeDto.ChordsOverLyrics].DefaultIncluded, byCode[IngredientCodeDto.Facts].Text).ShouldBe((false, "Tempo 114 BPM, 4/4, C major"));
        options.Notation.Select(choice => (choice.AssetId, choice.Kind, choice.DefaultSelected)).ShouldBe([(tab.Id, NotationKindDto.Tab, true), (score.Id, NotationKindDto.Score, true)]);
        options.Transpositions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Options_OfferTranspositionsForMelodic_AndTheCreateActions()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var melodic = await (await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Song(track.Id), HandoutFamilyDto.Melodic, TranspositionDto.BFlat), UriKind.Relative), Ct)).BodyAsync<HandoutOptionsDto>();
        var drums = await (await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Song(track.Id), HandoutFamilyDto.Drums), UriKind.Relative), Ct)).BodyAsync<HandoutOptionsDto>();
        var guitar = await (await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Song(track.Id), HandoutFamilyDto.Guitar), UriKind.Relative), Ct)).BodyAsync<HandoutOptionsDto>();
        var vocals = await (await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Song(track.Id), HandoutFamilyDto.Vocals), UriKind.Relative), Ct)).BodyAsync<HandoutOptionsDto>();

        (melodic.FamilyLabel, melodic.Transpositions.Count).ShouldBe(("Melodic instruments in B-flat", 4));
        Action(melodic, IngredientCodeDto.Notation).ShouldBe(IngredientActionDto.CreateTransposedPart);
        Action(drums, IngredientCodeDto.Notation).ShouldBe(IngredientActionDto.SplitDrums);
        Action(guitar, IngredientCodeDto.Notation).ShouldBe(IngredientActionDto.CreateTab);
        Action(vocals, IngredientCodeDto.Notation).ShouldBe(IngredientActionDto.CreateScore);
        Action(vocals, IngredientCodeDto.FormChart).ShouldBe(IngredientActionDto.DetectSections);
        Action(vocals, IngredientCodeDto.ChordsOverLyrics).ShouldBe(IngredientActionDto.DetectChords);
        drums.Ingredients.Select(item => item.Code).ShouldNotContain(IngredientCodeDto.ChordChart);
    }

    [Theory]
    [InlineData("/api/handouts/options?family=bass", "trackId")]
    [InlineData("/api/handouts/options?trackId=7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f&setlistId=7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f&family=bass", "trackId")]
    [InlineData("/api/handouts/options?trackId=7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f&family=tuba", "family")]
    [InlineData("/api/handouts/options?trackId=7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f&family=bass&transposition=b-flat", "transposition")]
    [InlineData("/api/handouts/options?trackId=7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f&family=melodic&transposition=g", "transposition")]
    public async Task Options_RefuseBadQueries(string url, string field)
    {
        using var response = await Client.GetAsync(new Uri(url, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "getHandoutOptions");
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Options_OfAnUnknownSongOrSetlist_Are404()
    {
        using var song = await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Song(Guid.NewGuid()), HandoutFamilyDto.Bass), UriKind.Relative), Ct);
        using var setlist = await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Setlist(Guid.NewGuid()), HandoutFamilyDto.Bass), UriKind.Relative), Ct);

        (song.StatusCode, setlist.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(song, "getHandoutOptions");
    }

    [Fact]
    public async Task OneClick_QueuesAHandoutItem_WithTheAssembledDocument()
    {
        var track = await Factory.SeedSongAsync();
        var (score, tab) = (HandoutTestData.Score(track.Id, ("bass", ScoreProfile.Bass), 1), HandoutTestData.Score(track.Id, ("bass", ScoreProfile.Bass), 2, HandoutTestData.Tab));
        await Factory.SeedAssetsAsync(score, tab);
        (await Client.PutAsJsonAsync(ApiRoutes.PracticeNote(track.Id, HandoutFamilyDto.Bass), new SetPracticeNoteRequest { Text = "Mute the open A." }, Ct)).EnsureSuccessStatusCode();

        using var response = await Client.PostHandoutAsync(Bass(track.Id));

        await ContractAssert.MatchesAsync(response, "createHandout");
        var asset = await response.BodyAsync<AssetDto>();
        (asset.Kind, asset.Status, asset.Handout, asset.Name).ShouldBe((AssetKindDto.Handout, AssetStatusDto.Queued, (HandoutFactsDto?)null, $"{track.Title} - Bass handout"));
        var recipe = asset.Provenance.HandoutRecipe!;
        (recipe.Kind, recipe.KindLabel, recipe.TrackId, recipe.Family, recipe.Paper, recipe.Include).ShouldBe(("handout", HandoutKindDto.Song, (Guid?)track.Id, HandoutFamilyDto.Bass, PaperDto.A4, (IReadOnlyList<IngredientCodeDto>?)null));
        (asset.Provenance.Origin, asset.Provenance.Recipe, asset.Provenance.ScoreRecipe).ShouldBe((AssetOriginDto.Handout, null, null));
        asset.Provenance.SourceAssetIds.ShouldBe([tab.Id, score.Id]);
        var sent = Factory.InsightsWorker.HandoutRequests.Single();
        (sent.Paper, sent.Document.Kind, sent.Document.FamilyLabel, sent.Document.GeneratedOn, sent.Document.Setlist).ShouldBe((Paper.A4, HandoutKind.Song, "Bass", "2026-11-14", (WorkerSetlistBlock?)null));
        sent.Document.Notice.ShouldBe(WorkerHandoutNotice.Fixed);
        var song = sent.Document.Songs.Single();
        song.Include.ShouldBe([IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.ChordChart, IngredientCode.Notation, IngredientCode.PracticeNotes]);
        song.Facts.ShouldBe(new WorkerSongFacts(114, "4/4", "C major", "chords", 213.04, null));
        (song.Sections.Count, song.Sections[0].Name, song.Lyrics, song.PracticeNotes).ShouldBe((5, "Intro", (WorkerHandoutLyrics?)null, "Mute the open A."));
        song.ChordSymbols.ShouldBe(["C", "G"]);
        song.Notation.ShouldBe([new WorkerHandoutNotation("Song 2", NotationKind.Tab, $"assets/{tab.Id}/score.pdf"), new WorkerHandoutNotation("Song 1", NotationKind.Score, $"assets/{score.Id}/score.pdf")]);
        sent.Document.Attributions.ShouldBe([HandoutNotice.ChordsLine]);
    }

    [Fact]
    public async Task ChordsOverLyrics_AreSent_WithTheLyricsSource()
    {
        var track = await Factory.SeedSongAsync();

        (await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Song(track.Id), Family = HandoutFamilyDto.Guitar })).Dispose();

        var song = Factory.InsightsWorker.HandoutRequests.Single().Document.Songs.Single();
        song.Lyrics!.ChordPlacement.ShouldBe("word");
        song.Lyrics.Source.ShouldStartWith("Lyrics: LRCLIB");
        song.Lyrics.Lines.ShouldNotBeEmpty();
        song.Lyrics.Lines.SelectMany(line => line.Chords).ShouldNotBeEmpty();
        Factory.InsightsWorker.HandoutRequests.Single().Document.Attributions[0].ShouldStartWith("Lyrics: LRCLIB");
    }

    [Fact]
    public async Task AMelodicPartInBFlat_IsTransposed()
    {
        var track = await Factory.SeedSongAsync();
        var part = HandoutTestData.Score(track.Id, ("other", ScoreProfile.Brass), 1, facts => facts with { Part = new PartFacts { Instrument = Domain.InstrumentViews.WrittenInstrument.TrumpetBb, Name = "Trumpet", Interval = "M2", Clef = Domain.InstrumentViews.ClefChoice.Treble } });
        await Factory.SeedAssetsAsync(part);

        using var response = await Client.PostHandoutAsync(new CreateHandoutRequest
        {
            Source = HandoutSourceDto.Song(track.Id),
            Family = HandoutFamilyDto.Melodic,
            Transposition = TranspositionDto.BFlat,
            Paper = PaperDto.Letter,
        });

        var asset = await response.BodyAsync<AssetDto>();
        asset.Name.ShouldEndWith("Melodic instruments in B-flat handout");
        var sent = Factory.InsightsWorker.HandoutRequests.Single();
        (sent.Paper, sent.Document.FamilyLabel).ShouldBe((Paper.Letter, "Melodic instruments in B-flat"));
        var song = sent.Document.Songs.Single();
        (song.Facts.Key, song.Facts.TranspositionNote).ShouldBe(("D major", "Part in B-flat (sounds a major second lower)"));
        song.ChordSymbols.ShouldBe(["D", "A"]);
        song.Notation.Single().Kind.ShouldBe(NotationKind.Transposed);
    }

    [Fact]
    public async Task Refusals_Are404_409_422_And503()
    {
        var processing = await Factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var song = await Factory.SeedSongAsync();

        using var missing = await Client.PostHandoutAsync(Bass(Guid.NewGuid()));
        using var notReady = await Client.PostHandoutAsync(Bass(processing.Id));
        using var drumsChords = await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Song(song.Id), Family = HandoutFamilyDto.Drums, Include = [IngredientCodeDto.ChordsOverLyrics] });
        using var both = await Client.PostHandoutAsync(new CreateHandoutRequest { Source = new HandoutSourceDto { TrackId = song.Id, SetlistId = Guid.NewGuid() }, Family = HandoutFamilyDto.Bass, Transposition = TranspositionDto.F, Name = " " });
        using var unknownScore = await Client.PostHandoutAsync(Bass(song.Id) with { NotationAssetIds = [Guid.NewGuid()] });
        Factory.InsightsWorker.Unavailable = true;
        using var down = await Client.PostHandoutAsync(Bass(song.Id));

        foreach (var response in new[] { missing, notReady, drumsChords, both, unknownScore, down })
        {
            await ContractAssert.MatchesAsync(response, "createHandout");
        }

        (missing.StatusCode, notReady.StatusCode, drumsChords.StatusCode, down.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity, HttpStatusCode.ServiceUnavailable));
        (await notReady.JsonAsync()).GetProperty("type").GetString().ShouldBe("https://stemstudio.local/problems/track-not-ready");
        (await drumsChords.FieldErrorsAsync("include")).ShouldBe(["chords-over-lyrics is not offered for the drums family."]);
        (await both.FieldErrorsAsync("source")).ShouldNotBeEmpty();
        (await both.FieldErrorsAsync("transposition")).ShouldNotBeEmpty();
        (await both.FieldErrorsAsync("name")).ShouldNotBeEmpty();
        (await unknownScore.FieldErrorsAsync("notationAssetIds")).ShouldNotBeEmpty();
        Factory.InsightsWorker.HandoutRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AWorkerRefusal_Is422_AndNothingIsStored()
    {
        var song = await Factory.SeedSongAsync();
        Factory.InsightsWorker.SubmitFailure = new WorkerRequestException(HttpStatusCode.UnprocessableEntity, "notice.footer must be the fixed notice text.");

        using var response = await Client.PostHandoutAsync(Bass(song.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("document")).Single().ShouldContain("notice.footer");
        using var list = await Client.GetAsync(new Uri(ApiRoutes.Assets + "?kind=handout", UriKind.Relative), Ct);
        (await list.JsonAsync()).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task TheSync_StoresThePdf_AndFillsAssetHandout()
    {
        var song = await Factory.SeedSongAsync();
        var created = await (await Client.PostHandoutAsync(Bass(song.Id))).BodyAsync<AssetDto>();
        var job = Factory.InsightsWorker.HandoutJobs.Keys.Single();

        Factory.InsightsWorker.SetHandout(job, WorkerRenderJobState.Running, 0.7);
        await Factory.SyncAssetsAsync();
        var rendering = (await Factory.FindAssetAsync(created.Id))!;
        Factory.InsightsWorker.CompleteHandout(job, FakeWorkerInsightsResults.WithWarnings());
        await Factory.SyncAssetsAsync();

        (rendering.Status, rendering.Progress).ShouldBe((AssetStatus.Rendering, 70));
        using var one = await Client.GetAsync(new Uri(ApiRoutes.Asset(created.Id), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(one, "getAsset");
        var ready = await one.BodyAsync<AssetDto>();
        var facts = ready.Handout!;
        (ready.Status, facts.Kind, facts.Family, facts.Pages, facts.Songs, facts.Paper, facts.Transposition).ShouldBe((AssetStatusDto.Ready, HandoutKindDto.Song, HandoutFamilyDto.Bass, 3, 1, PaperDto.A4, (TranspositionDto?)null));
        facts.Outline.ShouldBe(["Example Song", "Notation - Bass - score", "About this handout"]);
        facts.Warnings.Select(warning => warning.Code).ShouldBe([HandoutWarningCodeDto.NotationScaled, HandoutWarningCodeDto.NotationUnreadable, HandoutWarningCodeDto.MissingGlyphs]);
        facts.Omitted.ShouldContain(new OmittedIngredientDto { Code = IngredientCodeDto.Notation, Reason = OmissionReasonDto.NotAvailable });
        facts.Included.ShouldContain(IngredientCodeDto.ChordChart);
        ready.Files.Single().Role.ShouldBe(AssetFileRoleDto.Pdf);
        Factory.InsightsWorker.Deleted.ShouldBe([$"handout:{job}"]);
        using var file = await Client.GetAsync(new Uri(ApiRoutes.AssetFile(created.Id, "pdf"), UriKind.Relative), Ct);
        (await file.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Factory.InsightsWorker.Pdf);
        file.Content.Headers.ContentDisposition!.FileNameStar!.ShouldEndWith(" - Bass handout.pdf");
        using var list = await Client.GetAsync(new Uri(ApiRoutes.Assets + "?kind=handout", UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(list, "listAssets");
        (await list.JsonAsync()).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("damaged")]
    [InlineData("unusable")]
    [InlineData("lost")]
    [InlineData("failed")]
    public async Task ABrokenJob_FailsTheItem(string how)
    {
        var song = await Factory.SeedSongAsync();
        var created = await (await Client.PostHandoutAsync(Bass(song.Id))).BodyAsync<AssetDto>();
        var job = Factory.InsightsWorker.HandoutJobs.Keys.Single();
        var unusable = FakeWorkerInsightsResults.WithWarnings() with { Pages = 0 };
        Action fail = how switch
        {
            "damaged" => () => Factory.InsightsWorker.CompleteHandout(job, sha256: new string('0', 64)),
            "unusable" => () => Factory.InsightsWorker.CompleteHandout(job, unusable),
            "lost" => () => Factory.InsightsWorker.HandoutJobs.Clear(),
            _ => () => Factory.InsightsWorker.SetHandout(job, WorkerRenderJobState.Failed, 0.6, "The handout could not be written"),
        };

        fail();
        await Factory.SyncAssetsAsync();

        var asset = (await Factory.FindAssetAsync(created.Id))!;
        (asset.Status, asset.Files.Count).ShouldBe((AssetStatus.Failed, 0));
        asset.Error.ShouldBe(how switch
        {
            "damaged" => HandoutJobApplier.DamagedFile,
            "unusable" => HandoutJobApplier.InvalidResult,
            "lost" => AssetJobApplierLostJob,
            _ => "The handout could not be written",
        });
    }

    [Fact]
    public async Task MakeAgain_PostsTheRecipe_AndRetryResubmitsAFailedHandout()
    {
        var song = await Factory.SeedSongAsync();
        var first = await (await Client.PostHandoutAsync(Bass(song.Id) with { Include = [IngredientCodeDto.Facts], Paper = PaperDto.Letter })).BodyAsync<AssetDto>();
        var recipe = first.Provenance.HandoutRecipe!;

        using var again = await Client.PostHandoutAsync(new CreateHandoutRequest
        {
            Source = HandoutSourceDto.Song(recipe.TrackId!.Value), Family = recipe.Family, Include = recipe.Include, NotationAssetIds = recipe.NotationAssetIds, Transposition = recipe.Transposition, Paper = recipe.Paper,
        });
        var second = await again.BodyAsync<AssetDto>();
        Factory.InsightsWorker.SetHandout(Factory.InsightsWorker.HandoutJobs.Keys.Min(StringComparer.Ordinal)!, WorkerRenderJobState.Failed, 0, "boom");
        await Factory.SyncAssetsAsync();
        using var retry = await Client.PostAsync(new Uri(ApiRoutes.RetryAsset(first.Id), UriKind.Relative), null, Ct);

        second.Name.ShouldBe($"{song.Title} - Bass handout (2)");
        second.Provenance.HandoutRecipe!.Include.ShouldBe([IngredientCodeDto.Facts]);
        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await retry.BodyAsync<AssetDto>()).Status.ShouldBe(AssetStatusDto.Queued);
        Factory.InsightsWorker.HandoutRequests.Count.ShouldBe(3);
        Factory.InsightsWorker.HandoutRequests.Last().Paper.ShouldBe(Paper.Letter);
    }

    [Fact]
    public async Task AHandout_OutlivesItsTrack()
    {
        var song = await Factory.SeedSongAsync();
        var created = await (await Client.PostHandoutAsync(Bass(song.Id))).BodyAsync<AssetDto>();
        Factory.InsightsWorker.CompleteHandout(Factory.InsightsWorker.HandoutJobs.Keys.Single());
        await Factory.SyncAssetsAsync();

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Track(song.Id), UriKind.Relative), Ct);
        using var after = await Client.GetAsync(new Uri(ApiRoutes.Asset(created.Id), UriKind.Relative), Ct);

        deleted.IsSuccessStatusCode.ShouldBeTrue();
        var asset = await after.BodyAsync<AssetDto>();
        (asset.Status, asset.Provenance.TrackAvailable).ShouldBe((AssetStatusDto.Ready, false));
    }

    private const string AssetJobApplierLostJob = StemStudio.Api.Assets.AssetJobApplier.LostJob;

    private static IngredientActionDto? Action(HandoutOptionsDto options, IngredientCodeDto code) => options.Ingredients.Single(item => item.Code == code).Action;
}

/// <summary>Worker handout results built from the 1.11.0 examples.</summary>
internal static class FakeWorkerInsightsResults
{
    public static WorkerHandoutResult WithWarnings() => Worker.FakeWorkerInsightsClient.Example<WorkerHandoutJob>("handout-warnings-job").Result!;
}
