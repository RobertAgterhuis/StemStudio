using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Generations;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Voices;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;
using StemStudio.Contracts.Voices;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Songs;

/// <summary>010 T053: imported song and conversion takes carry their AI voice label (FR-013).</summary>
public sealed class SongImportOriginTests : ApiTestBase
{
    [Fact]
    public async Task ImportingASongTake_SetsTheVocalLabelAndTheSheet()
    {
        var song = await SongTakeTests.CompletedSongAsync(Factory, Client);
        var sheetId = (await (await Client.GetAsync(new Uri(ApiRoutes.Generation(song), UriKind.Relative), Ct)).BodyAsync<GenerationDto>()).Song!.Lyrics.LyricsId;

        using var response = await Client.PostAsync(new Uri(ApiRoutes.ImportTake(song, 1), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var track = await response.BodyAsync<TrackDto>();
        track.Origin.ShouldBe(new TrackOriginDto
        {
            Kind = TrackOriginKindDto.Generated,
            GenerationId = song,
            TakeIndex = 1,
            AiGenerated = true,
            Vocal = VocalLabelDto.Synthetic,
            LyricsSheetId = sheetId,
        });
        track.Title.ShouldBe("This little song - Jazz trio (take 1)");
        await ContractAssert.MatchesAsync(response, "importTake");
    }

    [Fact]
    public async Task ImportingAConvertedTake_CarriesTheProfileName()
    {
        var song = await SongTakeTests.CompletedSongAsync(Factory, Client);
        var profile = await (await VoiceProfileEndpointTests.CreateAsync(Client)).BodyAsync<VoiceProfileDto>();
        var conversion = await (await Client.PostAsJsonAsync(new Uri(ApiRoutes.VoiceConversions(song, 1), UriKind.Relative), new VoiceConversionRequest { ProfileId = profile.Id }, Ct)).BodyAsync<GenerationDto>();
        var trackId = conversion.VoiceConversion!.ImportedTrackId!.Value;
        await Factory.UpdateTrackAsync(trackId, track => track.Complete(SyncTestData.Completion(), SyncTestData.T0));
        var stems = Factory.Services.GetRequiredService<IStemFileStore>();
        foreach (var stem in StemCatalog.SixStem)
        {
            await stems.SaveStemAsync(trackId, stem, (s, ct) => s.WriteAsync(RestyleEndpointTests.StemBytes(stem), ct).AsTask(), CancellationToken.None);
        }

        await Factory.SyncGenerationsAsync();
        Factory.Singer.CompleteAs(Factory.Singer.Created.Single().Id, "CompletedVoiceConversion", [6, 6]);
        await Factory.SyncGenerationsAsync();

        using var response = await Client.PostAsync(new Uri(ApiRoutes.ImportTake(conversion.Id, 1), UriKind.Relative), null, Ct);

        var origin = (await response.BodyAsync<TrackDto>()).Origin!;
        (origin.Vocal, origin.VoiceProfileName, origin.Kind, origin.LyricsSheetId).ShouldBe((VocalLabelDto.Converted, "My voice", TrackOriginKindDto.Generated, null));
        await ContractAssert.MatchesAsync(response, "importTake");
    }
}
