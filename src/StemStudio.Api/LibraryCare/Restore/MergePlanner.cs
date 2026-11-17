using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Voices;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare.Restore;

/// <summary>
/// The rules of a merge restore (research R-07, US5): entity by entity in dependency order (artists, styles, lyrics sheets,
/// voice profiles, tracks, mixes, assets, generations, audio hashes, agent conversations). The same id in the live library
/// skips the item; the same track source key with another id skips the track with its mixes, assets and generations; a
/// taken unique name (style, voice profile, mix per track) is renamed "&lt;name&gt; (restored)". Agent settings and
/// <c>AgentSecrets</c> are never read from the archive (011 CK-FR-017).
/// </summary>
/// <param name="staged">Context on the staged (checked, migrated) database.</param>
/// <param name="live">Context on the live database (read only here).</param>
public sealed class MergePlanner(StemStudioDbContext staged, StemStudioDbContext live)
{
    /// <summary>Plans the merge.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The plan.</returns>
    public async Task<MergePlan> PlanAsync(CancellationToken cancellationToken)
    {
        var plan = new MergePlan();
        await ArtistsAsync(plan, cancellationToken);
        await StylesAsync(plan, cancellationToken);
        await LyricsSheetsAsync(plan, cancellationToken);
        await VoiceProfilesAsync(plan, cancellationToken);
        await TracksAsync(plan, cancellationToken);
        await MixesAsync(plan, cancellationToken);
        await AssetsAsync(plan, cancellationToken);
        await GenerationsAsync(plan, cancellationToken);
        await AudioHashesAsync(plan, cancellationToken);
        await new ConversationMerge(staged, live).PlanAsync(plan, cancellationToken);
        return plan;
    }

    /// <summary>Ids of a live query as a set.</summary>
    /// <param name="ids">The query.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The set.</returns>
    internal static async Task<HashSet<Guid>> IdsAsync(IQueryable<Guid> ids, CancellationToken cancellationToken) => [.. await ids.ToListAsync(cancellationToken)];

    /// <summary>Skips an item whose id is live (<c>same-id</c>) or whose parent was skipped (<c>dependency-skipped</c>).</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="item">The item.</param>
    /// <param name="liveIds">Live ids of its kind.</param>
    /// <param name="parentSkipped">True when a parent was skipped with its dependants.</param>
    /// <returns>True when skipped.</returns>
    internal static bool Skipped(MergePlan plan, MergeItem item, HashSet<Guid> liveIds, bool parentSkipped)
    {
        var reason = liveIds.Contains(item.Id) ? SkippedItemReasonDto.SameId : parentSkipped ? SkippedItemReasonDto.DependencySkipped : (SkippedItemReasonDto?)null;
        if (reason is { } why)
        {
            plan.Skip(item, why);
        }

        return reason is not null;
    }

    private static string Folder(string root, Guid id) => string.Create(CultureInfo.InvariantCulture, $"{root}/{id:D}");

    private static string[] TrackPaths(Track track) =>
        track.UploadFileName is { } upload ? [Folder("tracks", track.Id), "uploads/" + upload] : [Folder("tracks", track.Id)];

    private async Task ArtistsAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        var current = await IdsAsync(live.Artists.Select(artist => artist.Mbid), cancellationToken);
        plan.AddRows((await staged.Artists.AsNoTracking().ToListAsync(cancellationToken)).Where(artist => !current.Contains(artist.Mbid)));
    }

    private async Task StylesAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        var current = await live.Styles.AsNoTracking().Select(style => new { style.Id, style.Name }).ToListAsync(cancellationToken);
        var (ids, names) = (current.Select(style => style.Id).ToHashSet(), new TakenNames(current.Select(style => style.Name), StyleNaming.Normalize));
        foreach (var style in (await staged.Styles.AsNoTracking().ToListAsync(cancellationToken)).Where(style => !style.IsPreset))
        {
            var item = new MergeItem(MergeCategories.Styles, style.Id, style.Name);
            if (!Skipped(plan, item, ids, parentSkipped: false))
            {
                names.Take(item, StyleNaming.RestoredName, plan, style.RenameRestored);
                plan.Add(style, style.Id);
            }
        }
    }

    private async Task LyricsSheetsAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        var ids = await IdsAsync(live.LyricsSheets.Select(sheet => sheet.Id), cancellationToken);
        foreach (var sheet in await staged.LyricsSheets.AsNoTracking().ToListAsync(cancellationToken))
        {
            if (!Skipped(plan, new MergeItem(MergeCategories.LyricsSheets, sheet.Id, sheet.Title), ids, parentSkipped: false))
            {
                plan.Add(sheet, sheet.Id);
            }
        }
    }

    private async Task VoiceProfilesAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        var current = await live.VoiceProfiles.AsNoTracking().Select(profile => new { profile.Id, profile.DisplayName }).ToListAsync(cancellationToken);
        var (ids, names) = (current.Select(profile => profile.Id).ToHashSet(), new TakenNames(current.Select(profile => profile.DisplayName), VoiceNaming.Normalize));
        foreach (var profile in await staged.VoiceProfiles.AsNoTracking().ToListAsync(cancellationToken))
        {
            var item = new MergeItem(MergeCategories.VoiceProfiles, profile.Id, profile.DisplayName);
            if (!Skipped(plan, item, ids, parentSkipped: false))
            {
                names.Take(item, VoiceNaming.RestoredName, plan, profile.RenameRestored);
                plan.Add(profile, profile.Id, Folder("voices", profile.Id));
            }
        }
    }

    private async Task TracksAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        var current = await live.Tracks.AsNoTracking().Select(track => new { track.Id, track.Key }).ToListAsync(cancellationToken);
        var (ids, keys) = (current.Select(track => track.Id).ToHashSet(), current.Select(track => track.Key.Value).ToHashSet(StringComparer.Ordinal));
        foreach (var track in await staged.Tracks.AsNoTracking().Include(track => track.Lyrics).ToListAsync(cancellationToken))
        {
            var item = new MergeItem(MergeCategories.Tracks, track.Id, track.Title);
            if (!Skipped(plan, item, ids, parentSkipped: false))
            {
                AddTrack(plan, item, track, keys.Contains(track.Key.Value));
            }
        }
    }

    private static void AddTrack(MergePlan plan, MergeItem item, Track track, bool sameSource)
    {
        if (sameSource)
        {
            plan.Skip(item, SkippedItemReasonDto.SameSource);
            return;
        }

        plan.Add(track, track.Id, TrackPaths(track));
    }

    private async Task MixesAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        var current = await live.Mixes.AsNoTracking().Select(mix => new { mix.Id, mix.TrackId, mix.Name }).ToListAsync(cancellationToken);
        var (ids, liveNames) = (current.Select(mix => mix.Id).ToHashSet(), current.ToLookup(mix => mix.TrackId, mix => mix.Name));
        var names = new Dictionary<Guid, TakenNames>();
        foreach (var mix in await staged.Mixes.AsNoTracking().ToListAsync(cancellationToken))
        {
            var item = new MergeItem(MergeCategories.Mixes, mix.Id, mix.Name);
            if (!Skipped(plan, item, ids, plan.IsDropped(mix.TrackId)))
            {
                NamesOf(names, liveNames, mix.TrackId).Take(item, MixNaming.RestoredName, plan, mix.RenameRestored);
                plan.Add(mix, mix.Id);
            }
        }
    }

    private static TakenNames NamesOf(Dictionary<Guid, TakenNames> names, ILookup<Guid, string> liveNames, Guid trackId)
    {
        if (!names.TryGetValue(trackId, out var taken))
        {
            names[trackId] = taken = new TakenNames(liveNames[trackId], MixNaming.Normalize);
        }

        return taken;
    }

    private async Task AssetsAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        var ids = await IdsAsync(live.Assets.Select(asset => asset.Id), cancellationToken);
        foreach (var asset in await staged.Assets.AsNoTracking().ToListAsync(cancellationToken))
        {
            if (!Skipped(plan, new MergeItem(MergeCategories.Assets, asset.Id, asset.Name), ids, plan.IsDropped(asset.TrackId)))
            {
                plan.Add(asset, asset.Id, Folder("assets", asset.Id));
            }
        }
    }

    private async Task GenerationsAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        var ids = await IdsAsync(live.Generations.Select(generation => generation.Id), cancellationToken);
        var generations = (await staged.Generations.AsNoTracking().ToListAsync(cancellationToken)).OrderBy(generation => generation.VoiceConversion is not null);
        foreach (var generation in generations)
        {
            var item = new MergeItem(MergeCategories.Generations, generation.Id, generation.Restyle?.SourceTrackTitle);
            var parentSkipped = plan.IsDropped(generation.SourceTrackId) || plan.IsDropped(generation.VoiceConversion?.SourceGenerationId);
            if (!Skipped(plan, item, ids, parentSkipped))
            {
                plan.Add(generation, generation.Id, Folder("generations", generation.Id));
            }
        }
    }

    private async Task AudioHashesAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        var current = (await live.AudioHashes.AsNoTracking().Select(hash => hash.Sha256).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        plan.AddRows((await staged.AudioHashes.AsNoTracking().ToListAsync(cancellationToken)).Where(hash => !current.Contains(hash.Sha256) && plan.AddedIds.Contains(hash.OwnerId)));
    }
}

/// <summary>Report categories of the merge (the collection names of the manifest counts).</summary>
public static class MergeCategories
{
    /// <summary>Tracks.</summary>
    public const string Tracks = "tracks";

    /// <summary>Mixes.</summary>
    public const string Mixes = "mixes";

    /// <summary>User styles.</summary>
    public const string Styles = "styles";

    /// <summary>Lyrics sheets.</summary>
    public const string LyricsSheets = "lyricsSheets";

    /// <summary>Voice profiles (as the consent check of US2).</summary>
    public const string VoiceProfiles = "voiceProfiles";

    /// <summary>Library items.</summary>
    public const string Assets = "assets";

    /// <summary>Generations.</summary>
    public const string Generations = "generations";

    /// <summary>Agent conversations.</summary>
    public const string AgentConversations = "agentConversations";
}

/// <summary>Names in use for one unique-name scope; a taken name gets the "(restored)" variant (007 copy-name rule).</summary>
/// <param name="names">Names in the live library.</param>
/// <param name="normalize">The comparison form of a name.</param>
public sealed class TakenNames(IEnumerable<string> names, Func<string, string> normalize)
{
    private readonly List<string> _names = [.. names];

    /// <summary>Takes the item's name, renaming the item when the name is in use.</summary>
    /// <param name="item">The item with its archived name.</param>
    /// <param name="restoredName">The "(restored)" naming rule.</param>
    /// <param name="plan">The plan that reports the rename.</param>
    /// <param name="rename">Renames the staged entity.</param>
    public void Take(MergeItem item, Func<string, IEnumerable<string>, string> restoredName, MergePlan plan, Action<string> rename)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(restoredName);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(rename);
        var name = item.Name ?? string.Empty;
        if (_names.Exists(taken => normalize(taken) == normalize(name)))
        {
            name = restoredName(name, _names);
            rename(name);
            plan.Renamed(item, name);
        }

        _names.Add(name);
    }
}

/// <summary>Agent conversations of a merge with their messages, runs, steps, approvals, undo entries and events.</summary>
/// <param name="staged">The staged context.</param>
/// <param name="live">The live context.</param>
public sealed class ConversationMerge(StemStudioDbContext staged, StemStudioDbContext live)
{
    /// <summary>Plans the conversations.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task PlanAsync(MergePlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var ids = await MergePlanner.IdsAsync(live.AgentConversations.Select(conversation => conversation.Id), cancellationToken);
        var added = new HashSet<Guid>();
        foreach (var conversation in await staged.AgentConversations.AsNoTracking().ToListAsync(cancellationToken))
        {
            if (!MergePlanner.Skipped(plan, new MergeItem(MergeCategories.AgentConversations, conversation.Id, conversation.Title), ids, parentSkipped: false))
            {
                plan.Add(conversation, conversation.Id);
                added.Add(conversation.Id);
            }
        }

        await ChildrenAsync(plan, added, cancellationToken);
    }

    private static async Task<List<T>> RowsAsync<T>(IQueryable<T> rows, Func<T, bool> keep, CancellationToken cancellationToken)
        where T : class => [.. (await rows.AsNoTracking().ToListAsync(cancellationToken)).Where(keep)];

    private async Task ChildrenAsync(MergePlan plan, HashSet<Guid> conversations, CancellationToken cancellationToken)
    {
        plan.AddRows(await RowsAsync(staged.AgentMessages, message => conversations.Contains(message.ConversationId), cancellationToken));
        var runs = await RowsAsync(staged.AgentRuns, run => conversations.Contains(run.ConversationId), cancellationToken);
        var runIds = runs.Select(run => run.Id).ToHashSet();
        plan.AddRows(runs);
        plan.AddRows(await RowsAsync(staged.AgentRunSteps, step => runIds.Contains(step.RunId), cancellationToken));
        plan.AddRows(await RowsAsync(staged.AgentApprovals, approval => runIds.Contains(approval.RunId), cancellationToken));
        plan.AddRows(await RowsAsync(staged.AgentUndoEntries, entry => conversations.Contains(entry.ConversationId), cancellationToken));
        plan.AddRows(await RowsAsync(staged.AgentRunEvents, runEvent => runIds.Contains(runEvent.RunId), cancellationToken));
    }
}
