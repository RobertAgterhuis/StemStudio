using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Recording;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Sections;

namespace StemStudio.Api.Tests.Mixes;

/// <summary>
/// 014 T056 (<c>loop.sectionId</c>, FR-005) and 015 T061 (<c>MixSettings.take</c>, FR-012): save and load, an id the track
/// no longer has is read back as null with the rest kept, duplicate copies the take, deleting the take nulls it, and
/// clients without the fields are unaffected.
/// </summary>
public sealed class MixReferenceTests : RecordingTestBase
{
    private Task<HttpResponseMessage> CreateMixAsync(Guid trackId, MixSettingsDto settings) =>
        Client.PostAsJsonAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), new CreateMixRequest(trackId, settings), ApiFactory.Json, Ct);

    private async Task<MixDto> GetMixAsync(Guid mixId) =>
        (await Client.GetFromJsonAsync<MixDto>(new Uri(ApiRoutes.Mix(mixId), UriKind.Relative), ApiFactory.Json, Ct))!;

    private async Task<Guid> SectionAsync(Guid trackId) => await InScopeAsync(async context =>
    {
        var sections = TrackSections.Create(trackId, ApiFactory.Start.UtcDateTime);
        sections.ApplyDetected(new DetectionResult("stems-novelty/1.0.0", null, [], [new DetectedSection(default, "Intro", 0, 20, null, null)]), Guid.NewGuid, ApiFactory.Start.UtcDateTime);
        context.Add(sections);
        await context.SaveChangesAsync(Ct);
        return sections.Sections[0].Id;
    });

    private static MixSettingsDto WithLoop(Guid? sectionId) => MixEndpointsTests.Settings() with { Loop = new LoopRegionDto(10, 20, true, sectionId) };

    [Fact]
    public async Task LoopSectionId_RoundTrips()
    {
        var track = await TrackAsync();
        var sectionId = await SectionAsync(track.Id);

        using var created = await CreateMixAsync(track.Id, WithLoop(sectionId));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ContractAssert.MatchesAsync(created, "createMix");
        var mix = await created.BodyAsync<MixDto>();
        mix.Settings.Loop!.SectionId.ShouldBe(sectionId);
        (await GetMixAsync(mix.Id)).Settings.Loop!.SectionId.ShouldBe(sectionId);
    }

    [Fact]
    public async Task UnknownSectionId_IsReadBackAsNull_WithTheLoopKept()
    {
        var track = await TrackAsync();

        var mix = await (await CreateMixAsync(track.Id, WithLoop(Guid.NewGuid()))).BodyAsync<MixDto>();

        mix.Settings.Loop.ShouldBe(new LoopRegionDto(10, 20, true));
    }

    [Fact]
    public async Task ASectionDeletedLater_IsDroppedOnLoad()
    {
        var track = await TrackAsync();
        var sectionId = await SectionAsync(track.Id);
        var mix = await (await CreateMixAsync(track.Id, WithLoop(sectionId))).BodyAsync<MixDto>();
        await InScopeAsync(async context =>
        {
            context.RemoveRange(await context.Set<TrackSections>().ToListAsync(Ct));
            await context.SaveChangesAsync(Ct);
            return true;
        });

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getMix");
        var loaded = await response.BodyAsync<MixDto>();
        (loaded.Settings.Loop!.SectionId, loaded.Settings.Loop.Start).ShouldBe(((Guid?)null, 10.0));
        (await response.Content.ReadAsStringAsync(Ct)).ShouldNotContain("sectionId");
    }

    [Fact]
    public async Task ClientsWithoutTheNewFields_AreUnaffected()
    {
        var track = await TrackAsync();

        using var created = await CreateMixAsync(track.Id, MixEndpointsTests.Settings());

        var body = await created.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain("sectionId");
        body.ShouldNotContain("\"take\"");
    }

    [Fact]
    public async Task Take_SavesAndLoads_DuplicateCopiesIt_AndDeletingTheTakeNullsIt()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        var channel = new TakeChannelSettingsDto { TakeId = take.Id, Volume = 0.6, Pan = -0.5, Mute = false, Solo = true };

        using var created = await CreateMixAsync(track.Id, MixEndpointsTests.Settings() with { Take = channel });
        await ContractAssert.MatchesAsync(created, "createMix");
        var mix = await created.BodyAsync<MixDto>();
        using var duplicated = await Client.PostAsync(new Uri(ApiRoutes.DuplicateMix(mix.Id), UriKind.Relative), null, Ct);
        var copy = await duplicated.BodyAsync<MixDto>();

        mix.Settings.Take.ShouldBe(channel);
        (await GetMixAsync(mix.Id)).Settings.Take.ShouldBe(channel);
        copy.Settings.Take.ShouldBe(channel);

        (await Client.DeleteAsync(new Uri(ApiRoutes.RecordingTakeOf(take.Id), UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetMixAsync(mix.Id)).Settings.Take.ShouldBeNull();
        (await GetMixAsync(copy.Id)).Settings.Take.ShouldBeNull();
    }

    [Fact]
    public async Task StaleOrForeignTakeIds_AreNull_AndOutOfRangeValuesAre400LikeEveryMixValue()
    {
        var track = await TrackAsync();
        var other = await TrackAsync("bbbbbbbbbbb");
        var foreign = await CreateAsync(other.Id);

        var stale = await (await CreateMixAsync(track.Id, MixEndpointsTests.Settings("Stale") with { Take = new TakeChannelSettingsDto { TakeId = Guid.NewGuid(), Volume = 1, Pan = 0, Mute = false, Solo = false } })).BodyAsync<MixDto>();
        var otherTrack = await (await CreateMixAsync(track.Id, MixEndpointsTests.Settings("Foreign") with { Take = new TakeChannelSettingsDto { TakeId = foreign.Id, Volume = 1, Pan = 0, Mute = false, Solo = false } })).BodyAsync<MixDto>();
        var own = await CreateAsync(track.Id);
        using var invalid = await CreateMixAsync(track.Id, MixEndpointsTests.Settings("Loud") with { Take = new TakeChannelSettingsDto { TakeId = own.Id, Volume = 2, Pan = 0, Mute = false, Solo = false } });

        (stale.Settings.Take, otherTrack.Settings.Take).ShouldBe((null, null));
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(invalid, "createMix");
        (await invalid.Content.ReadAsStringAsync(Ct)).ShouldContain("take.volume");
    }

    [Fact]
    public async Task Update_KeepsAValidTake()
    {
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id);
        var mix = await (await CreateMixAsync(track.Id, MixEndpointsTests.Settings())).BodyAsync<MixDto>();
        var channel = new TakeChannelSettingsDto { TakeId = take.Id, Volume = 0.3, Pan = 0.2, Mute = true, Solo = false };

        using var updated = await Client.PutAsJsonAsync(new Uri(ApiRoutes.Mix(mix.Id), UriKind.Relative), MixEndpointsTests.Settings() with { Take = channel }, ApiFactory.Json, Ct);

        await ContractAssert.MatchesAsync(updated, "updateMix");
        (await updated.BodyAsync<MixDto>()).Settings.Take.ShouldBe(channel);
    }
}
