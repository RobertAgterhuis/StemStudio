using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Generations;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Songs;

/// <summary>010 T029: the section helper, song take labels and the song library title (FR-002, FR-011).</summary>
public sealed class SongTakeTests : ApiTestBase
{
    [Fact]
    public async Task Sections_ProposesMarkersWithoutStoring()
    {
        using var response = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSections, UriKind.Relative), new SectionsRequest
        {
            Text = "Sing it loud, sing it free\nEvery note is part of me\n\nThis little song belongs to me\n\nPaper planes and a borrowed tune\n\nThis little song belongs to me",
        }, Ct);

        var proposal = await response.BodyAsync<SectionProposalDto>();
        proposal.Text.ShouldStartWith("[verse]\nSing it loud");
        proposal.Sections.Select(s => (s.Marker, s.Number, s.LineCount)).ShouldBe([(SectionMarkerDto.Verse, null, 2), (SectionMarkerDto.Chorus, null, 1), (SectionMarkerDto.Verse, (int?)2, 1), (SectionMarkerDto.Chorus, null, 1)]);
        proposal.WordsPerSecondAt120s.ShouldBe(0.25);
        await ContractAssert.MatchesAsync(response, "proposeSections");
    }

    [Fact]
    public async Task Sections_EmptyTextIs400()
    {
        using var response = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSections, UriKind.Relative), new SectionsRequest { Text = " " }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(response, "proposeSections");
    }

    [Fact]
    public async Task CompletedSong_TakesAreSyntheticAndTitledAfterTheLyrics()
    {
        var generation = await CompletedSongAsync(Factory, Client);

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Generation(generation), UriKind.Relative), Ct);

        var take = (await response.BodyAsync<GenerationDto>()).Takes.Single();
        (take.Vocal, take.VoiceProfileName, take.VocalAudioUrl).ShouldBe((VocalLabelDto.Synthetic, null, null));
        var asset = await Factory.WithScopeAsync(async sp => (await sp.GetRequiredService<IAssetRepository>().FindAsync(take.LibraryAssetId!.Value, CancellationToken.None))!);
        asset.Name.ShouldBe("This little song - Jazz trio (take 1)");
        (asset.Provenance.TrackId, asset.Provenance.TrackTitle, asset.Provenance.Contents).ShouldBe((null, "This little song", "song, Jazz trio, take 1"));
        await ContractAssert.MatchesAsync(response, "getGeneration");
    }

    [Fact]
    public async Task CompletedSong_WithoutStyleIsTitledByTheLyricsOnly()
    {
        using var create = await Client.PostAsJsonAsync(new Uri(ApiRoutes.Songs, UriKind.Relative), new SongRequest
        {
            LyricsText = "la la", Title = "Mine", Language = "en", LeadVoice = LeadVoiceDto.Any, Instrumental = true,
        }, Ct);
        var generation = await create.BodyAsync<GenerationDto>();
        Factory.Generator.CompleteAs(Factory.Generator.Created[^1].Id, "CompletedInstrumental", [3, 3]);

        await Factory.SyncGenerationsAsync();

        var stored = await (await Client.GetAsync(new Uri(ApiRoutes.Generation(generation.Id), UriKind.Relative), Ct)).BodyAsync<GenerationDto>();
        stored.Takes.Single().Vocal.ShouldBe(VocalLabelDto.None);
        var asset = await Factory.WithScopeAsync(async sp => (await sp.GetRequiredService<IAssetRepository>().FindAsync(stored.Takes[0].LibraryAssetId!.Value, CancellationToken.None))!);
        asset.Name.ShouldBe("Mine (take 1)");
    }

    internal static async Task<Guid> CompletedSongAsync(ApiFactory factory, HttpClient client)
    {
        var sheet = await SongEndpointTests.SeedSheetAsync(factory);
        using var create = await client.PostAsJsonAsync(new Uri(ApiRoutes.Songs, UriKind.Relative), new SongRequest
        {
            LyricsId = sheet.Id, Language = "en", StyleId = RestyleEndpointTests.JazzTrio, LeadVoice = LeadVoiceDto.Female, DurationSeconds = 60,
        }, TestContext.Current.CancellationToken);
        var generation = await create.BodyAsync<GenerationDto>();
        factory.Generator.CompleteAs(factory.Generator.Created[^1].Id, "CompletedSong", [4, 4, 4]);
        await factory.SyncGenerationsAsync();
        return generation.Id;
    }
}
