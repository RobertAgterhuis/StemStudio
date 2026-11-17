using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>Cause of a new revision and the accept action that produced it.</summary>
/// <param name="Cause">Cause.</param>
/// <param name="AcceptGroupId">The accept action, for <see cref="RevisionCause.Accept"/>.</param>
public sealed record RevisionStamp(RevisionCause Cause, Guid? AcceptGroupId = null);

/// <summary>
/// Every text or title change of a sheet (user save, accept, undo, restore; FR-007, FR-008): stores the revision before and after,
/// re-anchors the open suggestions (a language change makes them stale) and saves in one unit of work.
/// </summary>
/// <param name="sheets">Sheets.</param>
/// <param name="revisions">Revisions.</param>
/// <param name="suggestions">Suggestions.</param>
/// <param name="clock">Clock.</param>
public sealed class LyricsSheetChanges(ILyricsSheetRepository sheets, ILyricsRevisionRepository revisions, ILyricSuggestionRepository suggestions, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Stores the first revision of a new sheet.</summary>
    /// <param name="sheet">The saved sheet.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task RecordCreatedAsync(LyricsSheet sheet, CancellationToken cancellationToken)
    {
        await revisions.AddAsync(LyricsSheetRevision.Of(sheet, RevisionCause.Created, null, Now), cancellationToken);
        await sheets.SaveAsync(cancellationToken);
    }

    /// <summary>Changes the sheet and records the change.</summary>
    /// <param name="sheet">The tracked sheet.</param>
    /// <param name="change">The change (increments the revision).</param>
    /// <param name="stamp">Cause and accept action of the new revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task ChangeAsync(LyricsSheet sheet, Action<LyricsSheet> change, RevisionStamp stamp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(change);
        var (oldText, oldLanguage) = (sheet.Text, sheet.Language);
        await revisions.AddAsync(LyricsSheetRevision.Of(sheet, sheet.LastCause, null, Now), cancellationToken);
        change(sheet);
        await revisions.AddAsync(LyricsSheetRevision.Of(sheet, stamp.Cause, stamp.AcceptGroupId, Now), cancellationToken);
        var resolver = new AnchorResolver(oldText, sheet.Text);
        // The query filters on the stored status; suggestions decided in this unit of work are already closed in memory.
        foreach (var open in (await suggestions.ForSheetAsync(sheet.Id, SuggestionStatus.Open, null, cancellationToken)).Where(s => s.IsOpen))
        {
            open.Reanchor(oldLanguage == sheet.Language ? resolver.Resolve(open.Anchor) : null);
        }

        await sheets.SaveAsync(cancellationToken);
    }

    /// <summary>The assistant state of a sheet for its responses.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The DTO.</returns>
    public async Task<SheetAssistInfoDto> InfoAsync(LyricsSheet sheet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return LyricAssistMapper.Info(sheet, await suggestions.CountOpenAsync(sheet.Id, cancellationToken));
    }
}
