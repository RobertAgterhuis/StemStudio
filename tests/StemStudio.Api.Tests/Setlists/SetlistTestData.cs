using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Setlists;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Setlists;

/// <summary>Helpers of the 014 setlist tests.</summary>
internal static class SetlistTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task<Mix> SeedMixAsync(this ApiFactory factory, Track track, string name = "Band night") => factory.WithScopeAsync(async sp =>
    {
        var settings = new MixSettings { Name = name, Metronome = new MetronomeSettings { Enabled = false, Volume = 0.7, BeatsPerBar = 4 } };
        var mix = Mix.Create(Guid.NewGuid(), track, settings, SyncTestData.T0);
        await sp.GetRequiredService<IMixRepository>().AddAsync(mix, CancellationToken.None);
        return mix;
    });

    public static async Task<SetlistDto> CreateSetlistAsync(this HttpClient client, string name = "Friday rehearsal", string note = "Bring the capo.")
    {
        using var response = await client.PostAsJsonAsync(ApiRoutes.Setlists, new SetlistCreateRequest { Name = name, Note = note }, Ct);
        response.EnsureSuccessStatusCode();
        return await response.BodyAsync<SetlistDto>();
    }

    public static Task<HttpResponseMessage> PutEntriesAsync(this HttpClient client, Guid setlistId, params SetlistEntryInputDto[] entries) =>
        client.PutAsJsonAsync(ApiRoutes.SetlistEntries(setlistId), new SetlistEntriesUpdateRequest { Entries = entries }, Ct);

    public static SetlistEntryInputDto Entry(Guid trackId, Guid? mixId = null, string note = "", Guid? id = null) =>
        new() { Id = id, TrackId = trackId, MixId = mixId, Note = note };
}
