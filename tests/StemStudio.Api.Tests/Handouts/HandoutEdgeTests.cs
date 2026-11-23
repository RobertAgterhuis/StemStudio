using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Contracts.Practice;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>Feature 020 T062/T064 edge cases: score key fallback, plain lyrics, practice tempo, own names, options texts, retry without the song.</summary>
public sealed class HandoutEdgeTests : ApiTestBase
{
    [Fact]
    public async Task WithoutChords_TheKeyComesFromTheScore_AndPlainLyricsAreSent()
    {
        var track = await Factory.SeedAsync(LyricsTestData.Found());
        await Factory.SeedAssetsAsync(HandoutTestData.Score(track.Id, ("bass", ScoreProfile.Bass), 1));

        using var response = await Client.PostHandoutAsync(new CreateHandoutRequest
        {
            Source = HandoutSourceDto.Song(track.Id),
            Family = HandoutFamilyDto.Bass,
            Include = [IngredientCodeDto.Facts, IngredientCodeDto.Lyrics, IngredientCodeDto.Notation],
            Name = "My bass sheet",
        });

        (await response.BodyAsync<AssetDto>()).Name.ShouldBe("My bass sheet");
        var song = Factory.InsightsWorker.HandoutRequests.Single().Document.Songs.Single();
        (song.Facts.Key, song.Facts.KeySource).ShouldBe(("A minor", "score"));
        (song.Lyrics!.ChordPlacement, song.Lyrics.Lines.SelectMany(line => line.Chords).Count()).ShouldBe(("none", 0));
        song.Include.ShouldBe([IngredientCode.Facts, IngredientCode.Lyrics, IngredientCode.Notation]);
    }

    [Fact]
    public async Task PracticeProgress_PutsTheBestTempoOnTheSections()
    {
        var track = await Factory.SeedSongAsync();
        var verse = (await Factory.FindSectionsAsync(track.Id))!.Sections[1];
        var session = new PracticeSessionInputDto
        {
            TrackId = track.Id,
            StartedAt = SyncTestData.T0.AddHours(-1),
            EndedAt = SyncTestData.T0.AddMinutes(-50),
            PlayedSeconds = 120,
            Sections = [new PracticeSectionInputDto { SectionId = verse.Id, Kind = PracticeSectionKindDto.Verse, Label = verse.Label, PlayedSeconds = 60, TrainerReachedPercent = 80 }],
        };
        (await Client.PutAsJsonAsync(ApiRoutes.PracticeSession(Guid.NewGuid()), session, Ct)).EnsureSuccessStatusCode();

        (await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Song(track.Id), Family = HandoutFamilyDto.Drums, Include = [IngredientCodeDto.PracticeProgress] })).Dispose();

        var sections = Factory.InsightsWorker.HandoutRequests.Single().Document.Songs.Single().Sections;
        sections[1].PracticeTempoPercent.ShouldBe(80);
        sections[0].PracticeTempoPercent.ShouldBeNull();
    }

    [Fact]
    public async Task OptionsOfASongStillProcessing_ShowTheLengthAndMissingData()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());

        var options = await (await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Song(track.Id), HandoutFamilyDto.Keys), UriKind.Relative), Ct)).BodyAsync<HandoutOptionsDto>();

        var byCode = options.Ingredients.ToDictionary(item => item.Code);
        byCode[IngredientCodeDto.Facts].Text.ShouldBe("Length 0:00");
        (byCode[IngredientCodeDto.Lyrics].Text, byCode[IngredientCodeDto.Lyrics].Action).ShouldBe(("No lyrics yet", (IngredientActionDto?)IngredientActionDto.FindLyrics));
        (byCode[IngredientCodeDto.ChordChart].Text, byCode[IngredientCodeDto.PracticeProgress].Text).ShouldBe(("No chords yet", "No practice log yet"));
        byCode[IngredientCodeDto.Notation].Text.ShouldBe("Notation not generated yet");
    }

    [Fact]
    public async Task PlainLyricsWithChords_AreNoTiming_InTheOptions()
    {
        var track = await Factory.SeedAsync(LyricsTestData.AlignPending());
        await Factory.SeedSongExtrasAsync(track.Id);

        var options = await (await Client.GetAsync(new Uri(ApiRoutes.HandoutOptions(HandoutSourceDto.Song(track.Id), HandoutFamilyDto.Guitar), UriKind.Relative), Ct)).BodyAsync<HandoutOptionsDto>();

        options.Ingredients.Single(item => item.Code == IngredientCodeDto.ChordsOverLyrics).Text.ShouldBe("No timing yet (beat grid or synced lyrics missing)");
    }

    [Fact]
    public async Task RetryingAHandoutWhoseSongIsGone_Is404()
    {
        var track = await Factory.SeedSongAsync();
        var created = await (await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Song(track.Id), Family = HandoutFamilyDto.Vocals })).BodyAsync<AssetDto>();
        Factory.InsightsWorker.SetHandout(Factory.InsightsWorker.HandoutJobs.Keys.Single(), WorkerRenderJobState.Failed, 0, "boom");
        await Factory.SyncAssetsAsync();
        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).Dispose();

        using var retry = await Client.PostAsync(new Uri(ApiRoutes.RetryAsset(created.Id), UriKind.Relative), null, Ct);

        retry.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
