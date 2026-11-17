using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Library;
using StemStudio.Api.Metadata;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Scores;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Library;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T030: filing in the same save and re-filing (FR-002, FR-008, FR-009, SC-001).</summary>
public sealed class LibraryFilerTests : ApiTestBase
{
    [Fact]
    public async Task Export_IsFiledInTheSameSave()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PostExportAsync(AssetTestData.Raw(track.Id, "drums"));

        await ContractAssert.MatchesAsync(response, "createAudioExport");
        var placement = (await response.BodyAsync<AssetDto>()).Placement!;
        (placement.Artist.Name, placement.Artist.Source, placement.Album.Name, placement.Song.Name, placement.Filed)
            .ShouldBe(("Rick Astley", PlacementSourceDto.Import, "Unknown album", "Never Gonna Give You Up", true));
        placement.Artist.FolderId.ShouldBe("arb385da54bfe8c6a9");
        placement.Song.FolderId.ShouldBe("soc02cef10277ea444");
    }

    [Fact]
    public async Task StemPack_FilesEveryItem()
    {
        var track = await Factory.SeedAsync(MatchedTrack());

        using var response = await Client.PostAsJsonAsync(new Uri(ApiRoutes.StemPacks, UriKind.Relative), new CreateStemPackRequest(track.Id, new ExportFormatDto(AudioFileFormatDto.Flac)), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var pack = await response.BodyAsync<StemPackDto>();
        pack.Items.Count.ShouldBe(6);
        pack.Items.ShouldAllBe(item => item.Placement!.Artist.Name == "Metallica" && item.Placement.Album.Source == PlacementSourceDto.Match && item.Placement.Year == 1991);
    }

    [Fact]
    public async Task Score_OfATrackChannel_IsFiledUnderTheTrack()
    {
        var track = await Factory.SeedAsync(MatchedTrack());

        using var response = await Client.PostScoreAsync(ScoreTestData.Request(track.Id, "bass"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.BodyAsync<AssetDto>()).Placement!.Song.Name.ShouldBe("Enter Sandman");
    }

    [Fact]
    public async Task Score_OfAnItemWhoseTrackIsGone_UsesTheItemsPlacement()
    {
        var track = await Factory.SeedAsync(MatchedTrack());
        var bass = await Factory.ReadyDrumsAsync(Client, track.Id);
        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).Dispose();

        using var response = await Client.PostScoreAsync(new CreateScoreRequest { Source = AudioSourceDto.FromAsset(bass.Id), Profile = ScoreProfileDto.Bass, Tempo = new ScoreTempoDto(120, 4, 0) });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.BodyAsync<AssetDto>()).Placement.ShouldBe(bass.Placement);
    }

    [Fact]
    public async Task ScoreFromAnUnfiledItem_ResolvesTheSourceFirst()
    {
        var track = await Factory.SeedAsync(MatchedTrack());
        var source = Asset.Create(Guid.NewGuid(), "drums", ExportProvenance(track.Id), T0);
        await Factory.AddUnfiledAsync(source);
        var score = Asset.CreateScore(Guid.NewGuid(), "drums (sheet music)", ExportProvenance(null) with { Recipe = null, SourceAssetIds = [source.Id], ScoreRecipe = ScoreRecipe() }, T0);

        await Factory.FileAsync(score);

        Level(score.Placement!.Artist).ShouldBe(("Metallica", PlacementSource.Match));
    }

    [Fact]
    public async Task GeneratedTake_WithASourceTrack_IsFiledUnderIt()
    {
        var track = await Factory.SeedAsync(MatchedTrack());
        var take = Generated(track.Id, GenerationKind.Restyle);

        await Factory.FileAsync(take);

        Level(take.Placement!.Song).ShouldBe(("Enter Sandman", PlacementSource.Match));
    }

    [Fact]
    public async Task GeneratedSong_WithoutATrack_IsFiledUnderMySongs()
    {
        var take = Generated(null, GenerationKind.Song);

        await Factory.FileAsync(take);

        (take.Placement!.Artist.Name, take.Placement.Album.Name, take.Placement.Song.Name).ShouldBe((LibraryKey.OwnSongsArtist, LibraryKey.OwnSongsAlbum, "Zomeravond"));
    }

    [Fact]
    public async Task Refile_AfterAFoundLookup_MovesAutomaticItemsOnly()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var (automatic, own) = (Asset.Create(Guid.NewGuid(), "a", ExportProvenance(track.Id), T0), Asset.Create(Guid.NewGuid(), "b", ExportProvenance(track.Id), T0));
        own.SetLibraryInfo(new LibraryInfo { Artist = "Mine" }, T0);
        await Factory.FileAsync(automatic, own);

        var moved = await RefileAsync(track.Id, t => Match(t, Found("Rick Astley", "Whenever You Need Somebody", "Never Gonna Give You Up", 1987)));

        moved.ShouldBe(2);
        var (a, b) = ((await Factory.FindAssetAsync(automatic.Id))!, (await Factory.FindAssetAsync(own.Id))!);
        (a.Placement!.Album.Name, a.Placement.Year).ShouldBe(("Whenever You Need Somebody", 1987));
        Level(b.Placement!.Artist).ShouldBe(("Mine", PlacementSource.Item));
        b.Placement.Album.Name.ShouldBe("Whenever You Need Somebody");
    }

    [Fact]
    public async Task Refile_WithoutChange_KeepsUpdatedAt()
    {
        var track = await Factory.SeedAsync(MatchedTrack());
        var item = Asset.Create(Guid.NewGuid(), "a", ExportProvenance(track.Id), T0);
        await Factory.FileAsync(item);
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));

        (await RefileAsync(track.Id, _ => { })).ShouldBe(0);

        (await Factory.FindAssetAsync(item.Id))!.UpdatedAt.ShouldBe(T0);
    }

    [Fact]
    public async Task MetadataApplier_RefilesAfterAFailedLookup()
    {
        var track = AssetTestData.NamedTrack();
        track.ScheduleLookup(new LookupQuery("Corrected title", "Corrected artist"), T0);
        track.StartLookup("0123456789abcdef0123456789abcdee", T0);
        await Factory.SeedAsync(track);
        var item = Asset.Create(Guid.NewGuid(), "a", ExportProvenance(track.Id), T0);
        await Factory.FileAsync(item);

        await Factory.WithScopeAsync(async sp =>
        {
            var tracks = sp.GetRequiredService<ITrackRepository>();
            var loaded = (await tracks.FindAsync(track.Id, Ct))!;
            await sp.GetRequiredService<MetadataJobApplier>().ApplyAsync(loaded, new WorkerMetadataJob { MetadataJobId = "0123456789abcdef0123456789abcdee", Status = WorkerMetadataJobState.Failed, Error = "boom" }, Ct);
            await tracks.SaveAsync(Ct);
            return true;
        });

        (await Factory.FindAssetAsync(item.Id))!.Placement!.Artist.Name.ShouldBe("Corrected artist");
    }

    [Fact]
    public async Task MetadataApplier_KeepsLibraryInfoSavedWhileTheLookupRan()
    {
        // The poller loads the track, then the user saves library info in another request, then the lookup ends: the
        // re-filing must use the saved names, not the stale track of the poller's context (E2E 022 run 1).
        var track = AssetTestData.NamedTrack();
        track.ScheduleLookup(new LookupQuery("Corrected title", "Corrected artist"), T0);
        track.StartLookup("0123456789abcdef0123456789abcdee", T0);
        await Factory.SeedAsync(track);
        var item = Asset.Create(Guid.NewGuid(), "a", ExportProvenance(track.Id), T0);
        await Factory.FileAsync(item);

        await Factory.WithScopeAsync(async sp =>
        {
            var tracks = sp.GetRequiredService<ITrackRepository>();
            var loaded = (await tracks.FindAsync(track.Id, Ct))!;
            (await Client.PutTrackInfoAsync(track.Id, LibraryInfoApi.Names(artist: "Own artist"))).StatusCode.ShouldBe(HttpStatusCode.OK);
            await sp.GetRequiredService<MetadataJobApplier>().ApplyAsync(loaded, new WorkerMetadataJob { MetadataJobId = "0123456789abcdef0123456789abcdee", Status = WorkerMetadataJobState.Failed, Error = "boom" }, Ct);
            await tracks.SaveAsync(Ct);
            return true;
        });

        var placement = (await Factory.FindAssetAsync(item.Id))!.Placement!;
        (placement.Artist.Name, placement.Artist.Source).ShouldBe(("Own artist", PlacementSource.Track));
        (await Factory.FindTrackAsync(track.Id))!.LibraryInfo.Artist.ShouldBe("Own artist");
    }

    [Fact]
    public async Task DeletedTrack_ItemsKeepTheirPlace()
    {
        var track = await Factory.SeedAsync(MatchedTrack());
        var item = Asset.Create(Guid.NewGuid(), "a", ExportProvenance(track.Id), T0);
        await Factory.FileAsync(item);
        var before = item.Placement;
        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).Dispose();

        var refiled = (await Factory.FindAssetAsync(item.Id))!;
        await Factory.WithScopeAsync(async sp =>
        {
            var tracked = (await sp.GetRequiredService<IAssetRepository>().FindAsync(item.Id, Ct))!;
            await sp.GetRequiredService<ILibraryFiler>().FileAsync(tracked, Ct);
            await sp.GetRequiredService<IAssetRepository>().SaveAsync(Ct);
            return true;
        });

        refiled.Placement.ShouldBe(before);
        (await Factory.FindAssetAsync(item.Id))!.Placement.ShouldBe(before);
    }

    [Fact]
    public async Task Filer_RejectsNull() => await Factory.WithScopeAsync(async sp =>
    {
        var filer = sp.GetRequiredService<ILibraryFiler>();
        await Should.ThrowAsync<ArgumentNullException>(() => filer.FileAsync(null!, Ct));
        await Should.ThrowAsync<ArgumentNullException>(() => filer.RefileTrackAsync(null!, Ct));
        await Should.ThrowAsync<ArgumentNullException>(() => filer.InputAsync(null!, Ct));
        return true;
    });

    private Task<int> RefileAsync(Guid trackId, Action<Track> change) => Factory.WithScopeAsync(async sp =>
    {
        var tracks = sp.GetRequiredService<ITrackRepository>();
        var track = (await tracks.FindAsync(trackId, Ct))!;
        change(track);
        var moved = await sp.GetRequiredService<ILibraryFiler>().RefileTrackAsync(track, Ct);
        await tracks.SaveAsync(Ct);
        return moved;
    });

    private static ScoreRecipe ScoreRecipe() => new()
    {
        Source = ScoreSource.FromAsset(Guid.NewGuid()),
        Profile = ScoreProfile.Bass,
        Resolution = ScoreResolution.Sixteenth,
    };

    private static Asset Generated(Guid? trackId, GenerationKind kind) => Asset.CreateGenerated(
        Guid.NewGuid(),
        new Provenance { Origin = AssetOrigin.Generation, TrackId = trackId, TrackTitle = "Zomeravond", Contents = "take 1", Generation = new GenerationSource(Guid.NewGuid(), 1, kind) },
        new GeneratedAudio("Zomeravond - take 1", [new AssetFile("audio", "audio.flac", "audio/flac", 10, new string('a', 64))], new AudioFacts(AudioFormat.Flac, 24, null, 10, -1, 0)),
        T0);
}
