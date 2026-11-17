using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;

namespace StemStudio.Web.Recording;

/// <summary>The passes of one Record press (FR-010): its takes, newest pass first.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="Takes">Its takes in panel order.</param>
public sealed record TakeSession(Guid SessionId, IReadOnlyList<TakeItem> Takes)
{
    /// <summary>True for a loop session (its takes carry pass numbers).</summary>
    public bool IsLoop => Takes.Any(t => t.PassIndex is not null);

    /// <summary>Saved takes that are not starred (deleted by "Delete the other passes").</summary>
    public IReadOnlyList<TakeItem> Unstarred => [.. Takes.Where(t => t.Take is { Favorite: false })];

    /// <summary>True when "Delete the other passes" applies: a pass is starred and others are not.</summary>
    public bool CanKeepStarred => IsLoop && Takes.Any(t => t.Take?.Favorite == true) && Unstarred.Count > 0;
}

/// <summary>Sessions, stars and batch deletion of <see cref="TakeLibrary"/> (FR-010, FR-014, US3).</summary>
public sealed partial class TakeLibrary
{
    /// <summary>The takes grouped by session, in panel order (newest session first).</summary>
    /// <returns>The sessions.</returns>
    public IReadOnlyList<TakeSession> Sessions() =>
        [.. _items.GroupBy(i => i.SessionId).Select(g => new TakeSession(g.Key, [.. g]))];

    /// <summary>Stars or un-stars a saved take.</summary>
    /// <param name="item">The take.</param>
    /// <returns>A task that completes when stored.</returns>
    public async Task ToggleStarAsync(TakeItem item)
    {
        var take = item.Take!;
        var (updated, error) = await ApiCall.TryAsync(() => api.UpdateTakeAsync(take.Id, new UpdateTakeRequest { Favorite = !take.Favorite }));
        Error = error;
        if (updated is not null)
        {
            item.Apply(updated);
        }
    }

    /// <summary>"Delete the other passes": deletes every saved, not starred take of the session.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="session">The session.</param>
    /// <returns>A task that completes when deleted.</returns>
    public async Task KeepStarredAsync(Guid trackId, TakeSession session)
    {
        var doomed = session.Unstarred;
        var ids = doomed.Select(t => t.Take!.Id).ToList();
        Error = await ApiCall.RunAsync(() => api.DeleteTakesAsync(trackId, ids));
        if (Error is null)
        {
            await RemoveAsync(doomed);
        }
    }

    private async Task RemoveAsync(IReadOnlyList<TakeItem> removed)
    {
        _items.RemoveAll(removed.Contains);
        if (Loaded is { } loaded && removed.Contains(loaded))
        {
            (Loaded, Compare) = (null, TakeCompareMode.Both);
            await engine.UnloadTakeAsync();
        }
    }
}
