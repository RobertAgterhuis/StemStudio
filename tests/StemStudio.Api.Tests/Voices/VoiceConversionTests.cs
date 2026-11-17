using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Generations;
using StemStudio.Api.Tests.Songs;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.Voices;

/// <summary>010 T046: "use my voice" conversions of song takes (FR-008, FR-011).</summary>
public sealed class VoiceConversionTests : ApiTestBase
{
    [Fact]
    public async Task Convert_ImportsTheTakeAndWaitsForItsStems()
    {
        var (song, profile) = await SongAndProfileAsync();

        using var response = await ConvertAsync(song, profile.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var conversion = await response.BodyAsync<GenerationDto>();
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.Generation(conversion.Id));
        (conversion.Kind, conversion.Status, conversion.Stage).ShouldBe((GenerationKindDto.VoiceConversion, GenerationStateDto.WaitingForStems, "Separating the take into stems"));
        var details = conversion.VoiceConversion!;
        (details.SourceGenerationId, details.SourceTakeIndex, details.VoiceProfileId, details.VoiceProfileName, details.PitchShift, details.AutoShift)
            .ShouldBe((song, 1, (Guid?)profile.Id, "My voice", -2, false));
        details.ImportedTrackId.ShouldNotBeNull();
        Worker.Uploads.Count.ShouldBe(1);
        Factory.Singer.Created.ShouldBeEmpty();
        await ContractAssert.MatchesAsync(response, "createVoiceConversion");
    }

    [Fact]
    public async Task Sync_SendsTheStemsAndTheSampleOnceTheTrackIsSeparated()
    {
        var (song, profile) = await SongAndProfileAsync();
        var conversion = await (await ConvertAsync(song, profile.Id)).BodyAsync<GenerationDto>();
        await Factory.SyncGenerationsAsync();
        Factory.Singer.Created.ShouldBeEmpty();

        await SeparateAsync(conversion.VoiceConversion!.ImportedTrackId!.Value);
        await Factory.SyncGenerationsAsync();

        var job = Factory.Singer.Created.Single();
        job.PartNames.ShouldBe(["target", "prompt", "accompaniment", "accompaniment", "accompaniment", "accompaniment", "accompaniment"]);
        job.Stems.Select(file => file.FileName).ShouldBe(["vocals.flac", "sample.flac", "drums.flac", "bass.flac", "guitar.flac", "piano.flac", "other.flac"]);
        job.Stems[1].Bytes.ShouldBe(Factory.Singer.NormalisedSample);
        var parameters = job.Params["voiceConversion"]!;
        (parameters["profileName"]!.GetValue<string>(), parameters["pitchShift"]!.GetValue<int>(), parameters["autoShift"]!.GetValue<bool>()).ShouldBe(("My voice", -2, false));
        (await GetAsync(conversion.Id)).Status.ShouldBe(GenerationStateDto.Queued);
        Factory.Generator.Created.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sync_CollectsTheConvertedTakeAndItsVocal()
    {
        var (song, profile) = await SongAndProfileAsync();
        var conversion = await (await ConvertAsync(song, profile.Id)).BodyAsync<GenerationDto>();
        await SeparateAsync(conversion.VoiceConversion!.ImportedTrackId!.Value);
        await Factory.SyncGenerationsAsync();
        Factory.Singer.CompleteAs(Factory.Singer.Created.Single().Id, "CompletedVoiceConversion", [6, 6]);

        await Factory.SyncGenerationsAsync();

        var stored = await GetAsync(conversion.Id);
        var take = stored.Takes.Single();
        (stored.Status, take.Vocal, take.VoiceProfileName).ShouldBe((GenerationStateDto.Completed, VocalLabelDto.Converted, "My voice"));
        take.VocalAudioUrl.ShouldBe(ApiRoutes.TakeVocal(conversion.Id, 1));
        using var vocal = await Client.GetAsync(new Uri(take.VocalAudioUrl!, UriKind.Relative), Ct);
        (await vocal.Content.ReadAsStringAsync(Ct)).ShouldStartWith("vocal:");
        using var missingVocal = await Client.GetAsync(new Uri(ApiRoutes.TakeVocal(song, 1), UriKind.Relative), Ct);
        missingVocal.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await Client.GetAsync(new Uri(ApiRoutes.VoiceProfile(profile.Id), UriKind.Relative), Ct)).BodyAsync<VoiceProfileDto>()).ConvertedTakeCount.ShouldBe(1);
        await ContractAssert.MatchesAsync(vocal, "getTakeVocal");
        await ContractAssert.MatchesAsync(missingVocal, "getTakeVocal");
        Factory.Singer.Deleted.ShouldBe([Factory.Singer.Created.Single().Id]);
    }

    [Fact]
    public async Task Convert_AnAlreadySeparatedTakeIsQueuedAtOnce()
    {
        var (song, profile) = await SongAndProfileAsync();
        var track = await (await Client.PostAsync(new Uri(ApiRoutes.ImportTake(song, 1), UriKind.Relative), null, Ct)).BodyAsync<Contracts.Tracks.TrackDto>();
        await SeparateAsync(track.Id);

        using var response = await ConvertAsync(song, profile.Id);

        (await response.BodyAsync<GenerationDto>()).Status.ShouldBe(GenerationStateDto.Queued);
        Factory.Singer.Created.Count.ShouldBe(1);
    }

    // Security review 013 item 1: a sample path outside the data directory is never sent to the singer.
    [Fact]
    public async Task Sync_ASamplePathOutsideTheDataDirectoryFailsTheConversion()
    {
        var (song, profile) = await SongAndProfileAsync();
        var conversion = await (await ConvertAsync(song, profile.Id)).BodyAsync<GenerationDto>();
        await Factory.WithScopeAsync(sp => sp.GetRequiredService<StemStudioDbContext>().Database
            .ExecuteSqlRawAsync("UPDATE VoiceProfiles SET SamplePath = '../../keys/key.xml'", Ct));
        await SeparateAsync(conversion.VoiceConversion!.ImportedTrackId!.Value);

        await Factory.SyncGenerationsAsync();

        var stored = await GetAsync(conversion.Id);
        (stored.Status, stored.Error!.Code).ShouldBe((GenerationStateDto.Failed, GenerationErrorCodeDto.EngineError));
        Factory.Singer.Created.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_AFailedSeparationFailsTheConversion()
    {
        var (song, profile) = await SongAndProfileAsync();
        var conversion = await (await ConvertAsync(song, profile.Id)).BodyAsync<GenerationDto>();
        await Factory.UpdateTrackAsync(conversion.VoiceConversion!.ImportedTrackId!.Value, track => track.Fail("separation failed", SyncTestData.T0));

        await Factory.SyncGenerationsAsync();

        var stored = await GetAsync(conversion.Id);
        (stored.Status, stored.Error!.Code).ShouldBe((GenerationStateDto.Failed, GenerationErrorCodeDto.EngineError));
    }

    [Fact]
    public async Task Convert_RefusesTakesWithoutAnAiVoiceAndBadRequests()
    {
        var (song, profile) = await SongAndProfileAsync();
        var restyle = await GenerationSyncServiceTests.CompletedAsync(Factory, Client, videoId: "otherTrack1");

        using var noVocals = await ConvertAsync(restyle, profile.Id);
        using var unknownProfile = await ConvertAsync(song, Guid.NewGuid());
        using var unknownTake = await Client.PostAsJsonAsync(new Uri(ApiRoutes.VoiceConversions(song, 2), UriKind.Relative), new VoiceConversionRequest { ProfileId = profile.Id }, Ct);
        using var pitch = await Client.PostAsJsonAsync(new Uri(ApiRoutes.VoiceConversions(song, 1), UriKind.Relative), new VoiceConversionRequest { ProfileId = profile.Id, PitchShift = 13 }, Ct);
        Factory.Singer.HealthExample = "NoGpuHealth";
        Factory.Clock.Advance(TimeSpan.FromSeconds(11));
        using var unavailable = await ConvertAsync(song, profile.Id);

        await RestyleEndpointTests.AssertProblemAsync(noVocals, HttpStatusCode.Conflict, "no-vocals");
        (unknownProfile.StatusCode, unknownTake.StatusCode, pitch.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.BadRequest));
        await RestyleEndpointTests.AssertProblemAsync(unavailable, HttpStatusCode.Conflict, "voice-unavailable");
        foreach (var response in new[] { noVocals, unknownProfile, unknownTake, pitch, unavailable })
        {
            await ContractAssert.MatchesAsync(response, "createVoiceConversion");
        }
    }

    internal async Task<(Guid Song, VoiceProfileDto Profile)> SongAndProfileAsync()
    {
        var song = await SongTakeTests.CompletedSongAsync(Factory, Client);
        var profile = await (await VoiceProfileEndpointTests.CreateAsync(Client)).BodyAsync<VoiceProfileDto>();
        return (song, profile);
    }

    internal Task<HttpResponseMessage> ConvertAsync(Guid generation, Guid profile) =>
        Client.PostAsJsonAsync(new Uri(ApiRoutes.VoiceConversions(generation, 1), UriKind.Relative), new VoiceConversionRequest { ProfileId = profile, PitchShift = -2, AutoShift = false }, Ct);

    internal async Task SeparateAsync(Guid trackId)
    {
        await Factory.UpdateTrackAsync(trackId, track => track.Complete(SyncTestData.Completion(), SyncTestData.T0));
        var stems = Factory.Services.GetRequiredService<IStemFileStore>();
        foreach (var stem in StemCatalog.SixStem)
        {
            await stems.SaveStemAsync(trackId, stem, (s, ct) => s.WriteAsync(RestyleEndpointTests.StemBytes(stem), ct).AsTask(), CancellationToken.None);
        }
    }

    private async Task<GenerationDto> GetAsync(Guid id) =>
        await (await Client.GetAsync(new Uri(ApiRoutes.Generation(id), UriKind.Relative), Ct)).BodyAsync<GenerationDto>();
}
