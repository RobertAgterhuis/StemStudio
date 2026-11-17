using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Voices;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.Recording;

/// <summary>
/// Feature 015 T054 and T066: batch delete, update (rename syncs the asset name, star, part), delete (cancels a running
/// render, removes files, hashes and mix references), the track-delete cascade, <c>Track.takeCount</c>, and the 013 write
/// gate on the deleting endpoints.
/// </summary>
public sealed class TakeEditAndDeleteTests : RecordingTestBase
{
    private Task<HttpResponseMessage> PatchAsync(Guid takeId, object body) =>
        Client.PatchAsJsonAsync(new Uri(ApiRoutes.RecordingTakeOf(takeId), UriKind.Relative), body, ApiFactory.Json, Ct);

    private Task<HttpResponseMessage> DeleteManyAsync(Guid trackId, params Guid[] ids) =>
        Client.PostAsJsonAsync(new Uri(ApiRoutes.TrackTakesDeleteOf(trackId), UriKind.Relative), new DeleteTakesRequest { TakeIds = ids }, ApiFactory.Json, Ct);

    private async Task<Guid> MixShowingAsync(Guid trackId, Guid takeId) => await InScopeAsync(async context =>
    {
        var track = await context.Tracks.SingleAsync(item => item.Id == trackId, Ct);
        var mix = Mix.Create(Guid.NewGuid(), track, new MixSettings { Name = "With take", Metronome = new MetronomeSettings { Enabled = false }, Take = new TakeChannelSettings { TakeId = takeId } }, ApiFactory.Start.UtcDateTime);
        context.Mixes.Add(mix);
        await context.SaveChangesAsync(Ct);
        return mix.Id;
    });

    [Fact]
    public async Task Rename_SyncsTheAssetName_StarAndPart_AndInvalidIs422()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);

        using var renamed = await PatchAsync(take.Id, new UpdateTakeRequest { Name = "  Best groove ", Favorite = true, Part = RecordingPartDto.Drums });
        using var invalid = await PatchAsync(take.Id, new UpdateTakeRequest { Name = new string('x', 81) });
        using var empty = await PatchAsync(take.Id, new { });
        using var missing = await PatchAsync(Guid.NewGuid(), new UpdateTakeRequest { Favorite = true });

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(renamed, "updateRecordingTake");
        var updated = await renamed.BodyAsync<RecordingTakeDto>();
        (updated.Name, updated.Favorite, updated.Part).ShouldBe(("Best groove", true, RecordingPartDto.Drums));
        (await AssetAsync(take.AssetId)).Name.ShouldBe("Best groove");
        invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(invalid, "updateRecordingTake");
        empty.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(missing, "updateRecordingTake");
    }

    [Fact]
    public async Task Delete_CancelsTheRender_RemovesFilesHashesAndMixReferences()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        var mixId = await MixShowingAsync(track.Id, take.Id);
        await InScopeAsync(async context =>
        {
            context.AudioHashes.Add(new AudioHash(new string('c', 64), AudioHashKind.Export, take.AssetId));
            await context.SaveChangesAsync(Ct);
            return true;
        });

        using var response = await Client.DeleteAsync(new Uri(ApiRoutes.RecordingTakeOf(take.Id), UriKind.Relative), Ct);
        using var again = await Client.DeleteAsync(new Uri(ApiRoutes.RecordingTakeOf(take.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.MatchesAsync(response, "deleteRecordingTake");
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Worker.RenderDeletes.ShouldBe([RenderJobId]);
        Directory.Exists(Path.Combine(AssetsDir, take.AssetId.ToString("D"))).ShouldBeFalse();
        (await TakesAsync()).ShouldBeEmpty();
        (await InScopeAsync(context => context.Assets.CountAsync(Ct))).ShouldBe(0);
        (await InScopeAsync(context => context.AudioHashes.CountAsync(hash => hash.OwnerId == take.AssetId, Ct))).ShouldBe(0);
        (await InScopeAsync(context => context.Mixes.SingleAsync(mix => mix.Id == mixId, Ct))).Take.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteMany_IgnoresUnknownAndOtherTrackIds_AndReturnsTheCount()
    {
        var track = await TrackAsync();
        var other = await TrackAsync("bbbbbbbbbbb");
        var first = await CreateAsync(track.Id);
        var second = await CreateAsync(track.Id);
        var kept = await CreateAsync(track.Id);
        var foreign = await CreateAsync(other.Id);

        using var response = await DeleteManyAsync(track.Id, first.Id, second.Id, foreign.Id, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "deleteRecordingTakes");
        (await response.BodyAsync<DeleteTakesResultDto>()).Deleted.ShouldBe(2);
        (await TakesAsync()).Select(take => take.Id).ShouldBe([kept.Id, foreign.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task DeleteMany_ValidatesTheIds_AndTheTrack()
    {
        var track = await TrackAsync();
        var id = Guid.NewGuid();

        using var none = await DeleteManyAsync(track.Id);
        using var repeated = await DeleteManyAsync(track.Id, id, id);
        using var unknown = await DeleteManyAsync(Guid.NewGuid(), id);

        none.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(none, "deleteRecordingTakes");
        repeated.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(unknown, "deleteRecordingTakes");
    }

    [Fact]
    public async Task TrackDelete_RemovesItsTakes_AndTheirFiles_AndTakeCountCounts()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        await CreateAsync(track.Id);
        CompleteRenders("fLaC"u8.ToArray());
        await Factory.SyncAssetsAsync();

        var dto = await Client.GetFromJsonAsync<TrackDto>(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), ApiFactory.Json, Ct);
        dto!.TakeCount.ShouldBe(2);
        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await TakesAsync()).ShouldBeEmpty();
        (await InScopeAsync(context => context.Assets.CountAsync(Ct))).ShouldBe(0);
        Directory.Exists(Path.Combine(AssetsDir, take.AssetId.ToString("D"))).ShouldBeFalse();
    }

    [Fact]
    public async Task TrackWithoutTakes_HasNoTakeCount()
    {
        var track = await TrackAsync();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("takeCount");
    }

    [Fact]
    public async Task DeletingEndpoints_AreRefusedWhileABackupRuns()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        var gate = Factory.Services.GetRequiredService<LibraryGate>();
        gate.TryBeginBackup().ShouldBeTrue();

        using var one = await Client.DeleteAsync(new Uri(ApiRoutes.RecordingTakeOf(take.Id), UriKind.Relative), Ct);
        using var many = await DeleteManyAsync(track.Id, take.Id);
        using var replace = await Client.PutAsync(new Uri(ApiRoutes.RecordingTakeAudioOf(take.Id), UriKind.Relative), Form(Wav(Samples(22_050)), new ReplaceTakeAudioMetadataDto { StartSample = 0, LengthSamples = 22_050, AdjustmentSamples = 0 }), Ct);

        foreach (var response in new[] { one, many, replace })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await ProblemTypeAsync(response)).ShouldBe(Problem("backup-running"));
        }

        gate.EndBackup();
        (await TakesAsync()).ShouldHaveSingleItem();
        _ = AssetTestData.RenderJobId;
        _ = typeof(MixSettingsDto);
        _ = typeof(StemStudioDbContext);
    }
}
