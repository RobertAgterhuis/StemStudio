using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using static StemStudio.Api.Tests.InstrumentViews.AnalysisTestData;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>Feature 016 T064: the chords operations (FR-010..FR-016), the track statuses and the instrument catalogue.</summary>
public sealed class ChordEndpointsTests : ApiTestBase
{
    private FakeWorkerAnalysisClient Analysis => Factory.AnalysisWorker;

    private static ChordChart LyricChart() => Chart() with
    {
        KeyRegions = [new KeyRegion(0, 16000, 0, KeyMode.Major, 0.9)],
        Segments =
        [
            new ChordSegment { StartMs = 0, EndMs = 10950, Label = "N", Confidence = 0.9 },
            new ChordSegment { StartMs = 10950, EndMs = 11500, Label = "C:maj", Root = 0, Quality = ChordQuality.Maj, Confidence = 0.9 },
            new ChordSegment { StartMs = 11500, EndMs = 14000, Label = "G:maj", Root = 7, Quality = ChordQuality.Maj, Confidence = 0.9 },
            new ChordSegment { StartMs = 14000, EndMs = 16000, Label = "A:min", Root = 9, Quality = ChordQuality.Min, Confidence = 0.9 },
        ],
    };

    [Fact]
    public async Task Get_UnknownTrack_Is404()
    {
        using var response = await Client.GetAsync(Url(ApiRoutes.Chords(Guid.NewGuid())), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_NeverRun_IsNoneWithTheLimits()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await Client.GetAsync(Url(ApiRoutes.Chords(track.Id)), Ct);

        await ContractAssert.MatchesAsync(response, "getChords");
        var dto = await response.BodyAsync<TrackAnalysisDto<ChordChartDocumentDto>>();
        (dto.Kind, dto.Status, dto.Document, dto.Engine, dto.Limits).ShouldBe((AnalysisKindDto.Chords, AnalysisStatusDto.None, null, null, InstrumentViewMapper.ChordLimits));
    }

    [Fact]
    public async Task Detect_Queues_ThenTheChartIsReady_AndTheTrackShowsStatusAndKey()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var queued = await Client.PostAsJsonAsync(ApiRoutes.Chords(track.Id), new DetectChordsRequest { Vocabulary = ChordVocabularyDto.Full }, Ct);

        queued.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(queued, "detectChords");
        (await queued.BodyAsync<TrackAnalysisDto<ChordChartDocumentDto>>()).ShouldSatisfyAllConditions(
            dto => dto.Status.ShouldBe(AnalysisStatusDto.Queued),
            dto => dto.EstimateSeconds.ShouldBe(12));
        Analysis.Requests.Single().ShouldBeOfType<Api.Worker.WorkerChordsRequest>().Vocabulary.ShouldBe("full");
        (await TrackAsync(track.Id)).InstrumentViews!.Chords.ShouldBe(AnalysisStatusDto.Queued);

        Analysis.Complete(Analysis.Jobs.Keys.Single());
        await Factory.SyncAnalysesAsync();

        using var ready = await Client.GetAsync(Url(ApiRoutes.Chords(track.Id)), Ct);
        await ContractAssert.MatchesAsync(ready, "getChords");
        var dto = await ready.BodyAsync<TrackAnalysisDto<ChordChartDocumentDto>>();
        (dto.Status, dto.Engine, dto.Device!.Kind, dto.EstimateSeconds).ShouldBe((AnalysisStatusDto.Ready, AnalysisEngineDto.Btc, ProcessingDeviceKindDto.Cpu, null));
        dto.Document!.Segments.Select(segment => segment.Label).ShouldBe(["N", "C:maj", "G:maj"]);
        var trackDto = await TrackAsync(track.Id);
        trackDto.InstrumentViews.ShouldBe(new InstrumentViewsStatusDto { Chords = AnalysisStatusDto.Ready, DrumHits = AnalysisStatusDto.Unavailable, VocalPitch = AnalysisStatusDto.None });
        trackDto.Key.ShouldBe(new TrackKeySummaryDto { Tonic = 0, Mode = KeyModeDto.Major, Confidence = 0.81, Display = "C major", Regions = 2 });
    }

    [Fact]
    public async Task TheTrackList_CarriesTheStatuses_AndOmitsThemWhenNothingApplies()
    {
        var queuedTrack = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var importing = await Factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        await Factory.SeedQueuedAsync(queuedTrack.Id, AnalysisKind.VocalPitch, JobId);

        var list = (await Client.GetFromJsonAsync<List<TrackDto>>(ApiRoutes.Tracks, ApiFactory.Json, Ct))!;

        list.Single(t => t.Id == queuedTrack.Id).InstrumentViews!.VocalPitch.ShouldBe(AnalysisStatusDto.Queued);
        list.Single(t => t.Id == importing.Id).InstrumentViews!.DrumHits.ShouldBe(AnalysisStatusDto.Unavailable, "no drums split yet");
        list.ShouldAllBe(t => t.Key == null);
    }

    [Fact]
    public async Task Detect_RefusesUnreadyTracksRunningAnalysesEditsAndADownWorker()
    {
        var importing = await Factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var edited = await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        await Factory.SeedReadyAsync(edited.Id, Chart(edited: true), new string('1', 64));
        var running = await Factory.SeedAsync(SyncTestData.CompletedTrack("ccccccccccc"));
        await Factory.SeedQueuedAsync(running.Id, AnalysisKind.Chords, JobId);

        await AssertProblemAsync(await PostAsync(importing.Id, null), HttpStatusCode.Conflict, InstrumentViewProblemTypes.TrackNotReady);
        await AssertProblemAsync(await PostAsync(edited.Id, new DetectChordsRequest()), HttpStatusCode.Conflict, InstrumentViewProblemTypes.EditsExist);
        await AssertProblemAsync(await PostAsync(running.Id, null), HttpStatusCode.Conflict, InstrumentViewProblemTypes.AnalysisRunning);
        (await PostAsync(Guid.NewGuid(), null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var overwrite = await PostAsync(edited.Id, new DetectChordsRequest { OverwriteEdits = true, Engine = ChordEngineDto.ChromaHmm });
        overwrite.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Analysis.Requests.Single().ShouldBeOfType<Api.Worker.WorkerChordsRequest>().Engine.ShouldBe("chroma-hmm");

        Analysis.Unavailable = true;
        var fresh = await Factory.SeedAsync(SyncTestData.CompletedTrack("ddddddddddd"));
        using var down = await PostAsync(fresh.Id, null);
        down.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(down, "detectChords");
    }

    [Fact]
    public async Task Detect_WithTheFakeEngine_Is422()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await PostAsync(track.Id, new DetectChordsRequest { Engine = ChordEngineDto.Fake });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "detectChords");
    }

    [Fact]
    public async Task Delete_CancelsAndClears_And404ForAnUnknownTrack()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedQueuedAsync(track.Id, AnalysisKind.Chords, JobId);

        using var deleted = await Client.DeleteAsync(Url(ApiRoutes.Chords(track.Id)), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.MatchesAsync(deleted, "deleteChords");
        Analysis.Deleted.ShouldBe([JobId]);
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.None);
        (await Client.DeleteAsync(Url(ApiRoutes.Chords(Guid.NewGuid())), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditSegment_SetsTheChordAndMarksItEdited()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedReadyAsync(track.Id, Chart(), new string('1', 64));

        using var response = await Client.PutAsJsonAsync(ApiRoutes.ChordSegment(track.Id, 2), new EditChordRequest { Label = "A:min7/b7" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "editChordSegment");
        var document = (await response.BodyAsync<TrackAnalysisDto<ChordChartDocumentDto>>()).Document!;
        document.HasEdits.ShouldBeTrue();
        document.Segments[2].ShouldBe(new ChordSegmentDto { StartMs = 2500, EndMs = 4500, Label = "A:min7/b7", Root = 9, Quality = ChordQualityDto.Min7, Bass = 7, Confidence = 0.88, Edited = true });
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!.HasEdits.ShouldBeTrue();
    }

    [Theory]
    [InlineData(2, "C:power")]
    [InlineData(2, "{title: x}")]
    [InlineData(3, "C:maj")]
    public async Task EditSegment_WithABadLabelOrIndex_Is422(int index, string label)
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedReadyAsync(track.Id, Chart(), new string('1', 64));

        using var response = await Client.PutAsJsonAsync(ApiRoutes.ChordSegment(track.Id, index), new EditChordRequest { Label = label }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "editChordSegment");
    }

    [Fact]
    public async Task EditSheetAndChordPro_WithoutReadyChords_Are409_And404ForAnUnknownTrack()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        await AssertProblemAsync(await Client.PutAsJsonAsync(ApiRoutes.ChordSegment(track.Id, 0), new EditChordRequest { Label = "C:maj" }, Ct), HttpStatusCode.Conflict, InstrumentViewProblemTypes.ChordsNotReady);
        await AssertProblemAsync(await Client.GetAsync(Url(ApiRoutes.ChordSheet(track.Id, 0, 0)), Ct), HttpStatusCode.Conflict, InstrumentViewProblemTypes.ChordsNotReady);
        await AssertProblemAsync(await Client.GetAsync(Url(ApiRoutes.ChordPro(track.Id, 0, 0)), Ct), HttpStatusCode.Conflict, InstrumentViewProblemTypes.ChordsNotReady);
        (await Client.GetAsync(Url(ApiRoutes.ChordSheet(Guid.NewGuid(), 0, 0)), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Sheet_PlacesTheChordsOverTheLyrics_SpelledForTransposeAndCapo()
    {
        var track = await Factory.SeedAsync(LyricsTestData.Found());
        await Factory.SeedReadyAsync(track.Id, LyricChart(), new string('1', 64));

        using var response = await Client.GetAsync(Url(ApiRoutes.ChordSheet(track.Id, 3, 1)), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getChordSheet");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var sheet = await response.BodyAsync<ChordSheetDto>();
        (sheet.Transpose, sheet.Capo).ShouldBe((3, 1));
        sheet.Rows.Select(row => (row.Text, string.Join(' ', row.Chords.Select(chord => $"{chord.CharIndex}:{chord.Symbol}")))).ShouldBe(
            [("Fake line one", "0:D 5:A"), ("La la la", "0:Bm")]);
    }

    [Fact]
    public async Task ChordPro_IsAnAttachmentWithDirectivesAndASafeName()
    {
        const string Title = "../{title: x}\r\n\"Song\"";
        var track = SyncTestData.UrlTrack();
        track.ApplyMetadata(new Domain.Tracks.SourceMetadata(Title, "Fake Artist"), T0);
        track.Complete(SyncTestData.Completion(), T0);
        track.ScheduleLookup(new Domain.Tracks.LookupQuery("Fake Song", "The Fixture Band", "Fixture Album", 262.0), T0);
        track.ScheduleLyricsLookup(T0);
        var found = LyricsTestData.Result("lyrics-result-word");
        track.Lyrics!.ApplyLookup(Enum.Parse<Domain.Lyrics.LyricsOutcome>(found.Outcome.ToString()), found.Lyrics, T0);
        await Factory.SeedAsync(track);
        await Factory.SeedReadyAsync(track.Id, LyricChart(), new string('1', 64));

        using var response = await Client.GetAsync(Url(ApiRoutes.ChordPro(track.Id, 0, 2)), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        OpenApiContract.Backend.Response("downloadChordPro", 200)!["content"]!["text/plain"].ShouldNotBeNull("plain text is not JSON, so the media type is checked only");
        response.Content.Headers.ContentType!.ToString().ShouldBe("text/plain; charset=utf-8");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        (response.Content.Headers.ContentDisposition.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName!.Trim('"')).ShouldBe("title_ x____Song.cho");
        var text = await response.Content.ReadAsStringAsync(Ct);
        text.ShouldStartWith("{title: ../(title: x)  \"Song\"}\n{artist: Fake Artist}\n{capo: 2}\n{key: Bb}\n");
        text.ShouldContain("[Bb]Fake [F]line one\n[Gm]La la la\n");
    }

    [Theory]
    [InlineData("Morning Light", "Morning Light.cho")]
    [InlineData("../../etc/passwd", "etc_passwd.cho")]
    [InlineData("a\"b;c\r\nd", "a_b_c__d.cho")]
    [InlineData("  ", "chords.cho")]
    [InlineData(null, "chords.cho")]
    public void FileName_KeepsOnlySafeCharacters(string? title, string name) => ChordService.FileName(title).ShouldBe(name);

    [Fact]
    public void FileName_IsCappedAt100Characters() => ChordService.FileName(new string('x', 300)).Length.ShouldBe(104);

    [Theory]
    [InlineData(13, 0)]
    [InlineData(-13, 0)]
    [InlineData(0, 13)]
    [InlineData(0, -1)]
    public async Task SheetAndChordPro_OutsideTheDisplayRange_Are422(int transpose, int capo)
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedReadyAsync(track.Id, Chart(), new string('1', 64));

        (await Client.GetAsync(Url(ApiRoutes.ChordSheet(track.Id, transpose, capo)), Ct)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Client.GetAsync(Url(ApiRoutes.ChordPro(track.Id, transpose, capo)), Ct)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Sheet_WithoutLyrics_UsesBarRows_AndAStaleChartStillShows()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var stale = await Factory.SeedReadyAsync(track.Id, Chart(), new string('1', 64));
        var stored = (await Factory.FindTrackAsync(track.Id))!;
        await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.RefreshStalenessAsync(stored, AnalysisKind.Chords, Ct));

        var sheet = await Client.GetFromJsonAsync<ChordSheetDto>(ApiRoutes.ChordSheet(track.Id, 0, 0), ApiFactory.Json, Ct);
        var chords = await Client.GetFromJsonAsync<TrackAnalysisDto<ChordChartDocumentDto>>(ApiRoutes.Chords(track.Id), ApiFactory.Json, Ct);

        sheet!.Rows.ShouldHaveSingleItem().Chords.Select(chord => chord.Symbol).ShouldBe(["C", "G"]);
        (chords!.Status, chords.Reason, chords.Document is null, stale.Kind).ShouldBe((AnalysisStatusDto.Stale, TrackAnalysis.StaleReason, false, AnalysisKind.Chords));
    }

    [Fact]
    public async Task TheAutoStarter_IsACompletionHook()
    {
        var hooks = await Factory.WithScopeAsync(sp => Task.FromResult(sp.GetServices<ITrackCompletionHook>().Select(hook => hook.GetType()).ToList()));
        var detailHooks = await Factory.WithScopeAsync(sp => Task.FromResult(sp.GetServices<IDetailCompletionHook>().Select(hook => hook.GetType()).ToList()));

        hooks.ShouldContain(typeof(AnalysisAutoStarter));
        detailHooks.ShouldBe([typeof(AnalysisAutoStarter)]);
    }

    [Fact]
    public async Task TheCatalogue_IsStaticAndCacheable()
    {
        using var response = await Client.GetAsync(Url(ApiRoutes.InstrumentCatalogue), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getInstrumentCatalogue");
        response.Headers.CacheControl!.MaxAge.ShouldBe(TimeSpan.FromHours(1));
        var catalogue = await response.BodyAsync<InstrumentCatalogueDto>();
        (catalogue.Families.Count, catalogue.Tunings.Count, catalogue.Instruments.Count, catalogue.Clefs.Count).ShouldBe((6, 12, 20, 7));
        catalogue.Instruments[0].ShouldBe(new WrittenInstrumentInfoDto { Id = WrittenInstrumentDto.Concert, Name = string.Empty, KeyName = "C", Interval = "P1", DefaultClef = "auto", LowMidi = 0, HighMidi = 127 });
        catalogue.Instruments.Single(i => i.Id == WrittenInstrumentDto.Viola).DefaultClef.ShouldBe("alto");
        catalogue.Families[5].ShouldSatisfyAllConditions(f => f.Id.ShouldBe(InstrumentFamilyDto.Orchestral), f => f.View.ShouldBe(InstrumentViewDto.Part));
        catalogue.Tunings[0].Pitches.ShouldBe([40, 45, 50, 55, 59, 64]);
        catalogue.Tolerances.ShouldBe(new SingingTolerancesDto());
    }

    [Fact]
    public void DomainAndContractEnums_ShareTheirOrder()
    {
        Names<ChordQuality, ChordQualityDto>().ShouldBeTrue();
        Names<DrumPiece, DrumPieceDto>().ShouldBeTrue();
        Names<AnalysisStatus, AnalysisStatusDto>().ShouldBeTrue();
        Names<AnalysisKind, AnalysisKindDto>().ShouldBeTrue();
        Names<AnalysisWarningCode, AnalysisWarningCodeDto>().ShouldBeTrue();
        Names<TuningId, TuningIdDto>().ShouldBeTrue();
        Names<WrittenInstrument, WrittenInstrumentDto>().ShouldBeTrue();
        Names<ClefChoice, ClefChoiceDto>().ShouldBeTrue();
        Names<InstrumentFamily, InstrumentFamilyDto>().ShouldBeTrue();
        Names<InstrumentView, InstrumentViewDto>().ShouldBeTrue();
    }

    private static bool Names<TDomain, TDto>()
        where TDomain : struct, Enum
        where TDto : struct, Enum =>
        Enum.GetNames<TDomain>().Select(Normal).SequenceEqual(Enum.GetNames<TDto>().Select(Normal));

    private static string Normal(string name) => name.ToUpperInvariant().Replace("HALFDIM", "HDIM", StringComparison.Ordinal);

    private static Uri Url(string route) => new(route, UriKind.Relative);

    private Task<HttpResponseMessage> PostAsync(Guid trackId, DetectChordsRequest? request) =>
        request is null ? Client.PostAsync(Url(ApiRoutes.Chords(trackId)), null, Ct) : Client.PostAsJsonAsync(ApiRoutes.Chords(trackId), request, Ct);

    private async Task<TrackDto> TrackAsync(Guid trackId) => (await Client.GetFromJsonAsync<TrackDto>(ApiRoutes.Track(trackId), ApiFactory.Json, Ct))!;

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(status);
            (await response.Content.ReadAsStringAsync(Ct)).ShouldContain($"/problems/{code}");
        }
    }
}
