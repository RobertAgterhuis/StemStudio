using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>Server tool handlers of feature 001 (tracks, imports, mixes) over <c>IStemStudioActions</c>.</summary>
internal static class Core001Handlers
{
    private const int MaxTracks = 10;
    private const int MaxMixes = 20;

    public static async Task<ToolResult> SearchLibraryAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var query = Fold(call.Text("query"));
        var status = call.Text("status");
        var tracks = await call.Actions.Tracks.ListTracksAsync(cancellationToken);
        var matches = tracks
            .Where(track => query.Length == 0 || Fold(track.Title).Contains(query, StringComparison.Ordinal) || Fold(track.Artist).Contains(query, StringComparison.Ordinal))
            .Where(track => status is null || TrackProjections.LibraryStatus(track.Status) == status)
            .Take(MaxTracks);
        return ToolResult.Of(new JsonArray([.. matches.Select(TrackProjections.Summary)]));
    }

    public static async Task<ToolResult> GetTrackAsync(ToolInvocation call, CancellationToken cancellationToken) =>
        ToolResult.Of(TrackProjections.Details(await call.Actions.Tracks.GetTrackAsync(call.RequiredId("trackId"), cancellationToken)));

    public static async Task<ToolResult> ImportFromUrlAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var track = await call.Actions.Tracks.ImportFromUrlAsync(call.Text("url")!, cancellationToken);
        var job = new JobRef { TrackId = track.Id, Phase = JobPhaseDto.Standard };
        var projection = new JsonObject
        {
            ["trackId"] = track.Id.ToString(),
            ["status"] = Name(track.Status),
            ["cached"] = track.Status == TrackStatusDto.Completed,
            ["jobRef"] = JobRef(job),
        };
        return new ToolResult { Projection = projection, Job = job, Undo = Undo("delete_track", new JsonObject { ["trackId"] = track.Id.ToString() }) };
    }

    public static async Task<ToolResult> RetryTrackAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var track = await call.Actions.Tracks.RetryTrackAsync(call.RequiredId("trackId"), cancellationToken);
        var job = new JobRef { TrackId = track.Id, Phase = JobPhaseDto.Standard };
        return new ToolResult { Projection = new JsonObject { ["trackId"] = track.Id.ToString(), ["status"] = Name(track.Status), ["jobRef"] = JobRef(job) }, Job = job };
    }

    public static async Task<ToolResult> DeleteTrackAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        await call.Actions.Tracks.DeleteTrackAsync(call.RequiredId("trackId"), cancellationToken);
        return ToolResult.Of(new JsonObject { ["deleted"] = true });
    }

    public static async Task<ToolResult> ListMixesAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var mixes = await call.Actions.Mixes.ListMixesAsync(call.Id("trackId"), cancellationToken);
        return ToolResult.Of(new JsonArray([.. mixes.Take(MaxMixes).Select(mix => (JsonNode)new JsonObject
        {
            ["mixId"] = mix.Id.ToString(),
            [ResultProjector.UntrustedField] = Untrusted(mix.Name),
            ["trackId"] = mix.TrackId.ToString(),
            ["updatedAt"] = mix.UpdatedAt,
        })]));
    }

    public static async Task<ToolResult> GetMixAsync(ToolInvocation call, CancellationToken cancellationToken) =>
        ToolResult.Of(MixProjections.Mix(await call.Actions.Mixes.GetMixAsync(call.RequiredId("mixId"), cancellationToken)));

    public static async Task<ToolResult> SaveMixAsAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var settings = await MixProjections.ReadOpenMixerAsync(call, cancellationToken);
        var mix = await call.Actions.Mixes.CreateMixAsync(new CreateMixRequest(call.RequiredId("trackId"), settings with { Name = call.Text("name")! }), cancellationToken);
        return new ToolResult
        {
            Projection = new JsonObject { ["mixId"] = mix.Id.ToString(), ["name"] = call.Text("name") },
            Undo = Undo("delete_mix", new JsonObject { ["mixId"] = mix.Id.ToString() }),
            UndoFresh = true,
        };
    }

    public static async Task<ToolResult> UpdateMixAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var mixId = call.RequiredId("mixId");
        var previous = await call.Actions.Mixes.GetMixAsync(mixId, cancellationToken);
        var settings = await MixProjections.ReadOpenMixerAsync(call, cancellationToken);
        var mix = await call.Actions.Mixes.UpdateMixAsync(mixId, settings with { Name = previous.Settings.Name }, cancellationToken);
        var undo = new JsonObject { ["mixId"] = mixId.ToString(), ["settings"] = JsonSerializer.SerializeToNode(previous.Settings, Json) };
        return new ToolResult { Projection = new JsonObject { ["mixId"] = mix.Id.ToString(), ["updatedAt"] = mix.UpdatedAt }, Undo = Undo("update_mix", undo) };
    }

    public static async Task<ToolResult> DuplicateMixAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var mix = await call.Actions.Mixes.DuplicateMixAsync(call.RequiredId("mixId"), cancellationToken);
        return new ToolResult
        {
            Projection = new JsonObject { ["mixId"] = mix.Id.ToString(), [ResultProjector.UntrustedField] = Untrusted(mix.Settings.Name) },
            Undo = Undo("delete_mix", new JsonObject { ["mixId"] = mix.Id.ToString() }),
            UndoFresh = true,
        };
    }

    public static async Task<ToolResult> RenameMixAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var mixId = call.RequiredId("mixId");
        var previous = await call.Actions.Mixes.GetMixAsync(mixId, cancellationToken);
        var mix = await call.Actions.Mixes.UpdateMixAsync(mixId, previous.Settings with { Name = call.Text("name")! }, cancellationToken);
        return new ToolResult
        {
            Projection = new JsonObject { ["mixId"] = mix.Id.ToString(), ["name"] = call.Text("name") },
            Undo = Undo("rename_mix", new JsonObject { ["mixId"] = mixId.ToString(), ["name"] = previous.Settings.Name }),
        };
    }

    public static async Task<ToolResult> DeleteMixAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        await call.Actions.Mixes.DeleteMixAsync(call.RequiredId("mixId"), cancellationToken);
        return ToolResult.Of(new JsonObject { ["deleted"] = true });
    }
}
