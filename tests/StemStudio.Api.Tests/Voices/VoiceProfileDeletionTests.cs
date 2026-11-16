using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;

namespace StemStudio.Api.Tests.Voices;

/// <summary>010 T048: deleting a voice profile removes every file derived from its sample (SC-006, FR-009).</summary>
public sealed class VoiceProfileDeletionTests : ApiTestBase
{
    [Fact]
    public async Task Delete_KeepsConvertedTakesLabelledProfileDeleted()
    {
        var (conversion, profile) = await CompletedConversionAsync();

        using var response = await DeleteAsync(profile, deleteTakes: false);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var stored = await GetAsync(conversion);
        (stored.VoiceConversion!.VoiceProfileId, stored.VoiceConversion.VoiceProfileName).ShouldBe((null, "My voice (profile deleted)"));
        stored.Takes.Single().VoiceProfileName.ShouldBe("My voice (profile deleted)");
        Directory.Exists(Path.Combine(Factory.DataDir, "voices", profile.ToString())).ShouldBeFalse();
        NoFileHoldsTheSample();
        (await Client.GetAsync(new Uri(ApiRoutes.VoiceProfile(profile), UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "deleteVoiceProfile");
    }

    [Fact]
    public async Task Delete_WithTakesRemovesTheConversions()
    {
        var (conversion, profile) = await CompletedConversionAsync();

        using var response = await DeleteAsync(profile, deleteTakes: true);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Client.GetAsync(new Uri(ApiRoutes.Generation(conversion), UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Directory.Exists(Path.Combine(Factory.DataDir, "generations", conversion.ToString())).ShouldBeFalse();
        NoFileHoldsTheSample();
    }

    [Fact]
    public async Task Delete_CancelsActiveConversionsOnTheSinger()
    {
        var (song, profile) = await SongAndProfileAsync();
        var conversion = await (await ConvertAsync(song, profile)).BodyAsync<GenerationDto>();
        await SeparateAsync(conversion.VoiceConversion!.ImportedTrackId!.Value);
        await Factory.SyncGenerationsAsync();
        var job = Factory.Singer.Created.Single().Id;

        using var response = await DeleteAsync(profile, deleteTakes: false);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.Singer.Deleted.ShouldBe([job]);
        (await GetAsync(conversion.Id)).Status.ShouldBe(GenerationStateDto.Cancelled);
    }

    [Fact]
    public async Task Delete_UnknownIs404()
    {
        using var response = await DeleteAsync(Guid.NewGuid(), deleteTakes: false);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "deleteVoiceProfile");
    }

    private void NoFileHoldsTheSample()
    {
        var sample = Factory.Singer.NormalisedSample;
        foreach (var file in Directory.EnumerateFiles(Factory.DataDir, "*", SearchOption.AllDirectories).Where(path => !path.EndsWith(".db", StringComparison.Ordinal) && !path.Contains(".db-", StringComparison.Ordinal)))
        {
            var bytes = File.ReadAllBytes(file);
            bytes.SequenceEqual(sample).ShouldBeFalse(file);
            bytes.SequenceEqual(VoiceProfileEndpointTests.OwnVoice).ShouldBeFalse(file);
        }
    }

    private async Task<(Guid Conversion, Guid Profile)> CompletedConversionAsync()
    {
        var (song, profile) = await SongAndProfileAsync();
        var conversion = await (await ConvertAsync(song, profile)).BodyAsync<GenerationDto>();
        await SeparateAsync(conversion.VoiceConversion!.ImportedTrackId!.Value);
        await Factory.SyncGenerationsAsync();
        Factory.Singer.CompleteAs(Factory.Singer.Created.Single().Id, "CompletedVoiceConversion", [6, 6]);
        await Factory.SyncGenerationsAsync();
        return (conversion.Id, profile);
    }

    private async Task<(Guid Song, Guid Profile)> SongAndProfileAsync()
    {
        var song = await Songs.SongTakeTests.CompletedSongAsync(Factory, Client);
        var profile = await (await VoiceProfileEndpointTests.CreateAsync(Client)).BodyAsync<Contracts.Voices.VoiceProfileDto>();
        return (song, profile.Id);
    }

    private Task<HttpResponseMessage> ConvertAsync(Guid song, Guid profile) => Client.PostAsync(
        new Uri(ApiRoutes.VoiceConversions(song, 1), UriKind.Relative), System.Net.Http.Json.JsonContent.Create(new VoiceConversionRequest { ProfileId = profile }), Ct);

    private async Task SeparateAsync(Guid trackId)
    {
        await Factory.UpdateTrackAsync(trackId, track => track.Complete(SyncTestData.Completion(), SyncTestData.T0));
        var stems = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<StemStudio.Api.Storage.IStemFileStore>(Factory.Services);
        foreach (var stem in StemStudio.Domain.Tracks.StemCatalog.SixStem)
        {
            await stems.SaveStemAsync(trackId, stem, (s, ct) => s.WriteAsync(Generations.RestyleEndpointTests.StemBytes(stem), ct).AsTask(), CancellationToken.None);
        }
    }

    private Task<HttpResponseMessage> DeleteAsync(Guid profile, bool deleteTakes) =>
        Client.DeleteAsync(new Uri($"{ApiRoutes.VoiceProfile(profile)}?deleteTakes={(deleteTakes ? "true" : "false")}", UriKind.Relative), Ct);

    private async Task<GenerationDto> GetAsync(Guid id) =>
        await (await Client.GetAsync(new Uri(ApiRoutes.Generation(id), UriKind.Relative), Ct)).BodyAsync<GenerationDto>();
}
