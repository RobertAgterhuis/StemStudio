using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Persistence.Repositories;

/// <summary>Persistence of lyric assists and their events (feature 023).</summary>
public interface ILyricAssistRepository
{
    /// <summary>One assist (tracked).</summary>
    /// <param name="id">Assist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The assist or null.</returns>
    Task<AssistRun?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The newest assists of a sheet, newest first.</summary>
    /// <param name="sheetId">Sheet id.</param>
    /// <param name="take">Maximum.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The assists.</returns>
    Task<IReadOnlyList<AssistRun>> ListForSheetAsync(Guid sheetId, int take, CancellationToken cancellationToken);

    /// <summary>The queued or running assists (tracked).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The active assists, oldest first.</returns>
    Task<IReadOnlyList<AssistRun>> ActiveAsync(CancellationToken cancellationToken);

    /// <summary>Inserts an assist and saves.</summary>
    /// <param name="assist">The assist.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(AssistRun assist, CancellationToken cancellationToken);

    /// <summary>Deletes the assists of a sheet beyond the newest <paramref name="keep"/> that have no open suggestions.</summary>
    /// <param name="sheetId">Sheet id.</param>
    /// <param name="keep">Assists kept.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Deleted count.</returns>
    Task<int> PruneAsync(Guid sheetId, int keep, CancellationToken cancellationToken);

    /// <summary>Appends an event and saves every tracked change.</summary>
    /// <param name="assistEvent">The event.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddEventAsync(AssistEvent assistEvent, CancellationToken cancellationToken);

    /// <summary>The events of an assist after a sequence number.</summary>
    /// <param name="assistId">Assist id.</param>
    /// <param name="after">Sequence number (0 = all).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The events in order.</returns>
    Task<IReadOnlyList<AssistEvent>> EventsAsync(Guid assistId, int after, CancellationToken cancellationToken);

    /// <summary>The highest event sequence number of an assist (0 when none).</summary>
    /// <param name="assistId">Assist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sequence number.</returns>
    Task<int> LastSeqAsync(Guid assistId, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>Reloads a tracked assist from the database (another scope may have changed it).</summary>
    /// <param name="assist">The assist.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task ReloadAsync(AssistRun assist, CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ILyricAssistRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class LyricAssistRepository(StemStudioDbContext context) : ILyricAssistRepository
{
    private static readonly AssistStatus[] Active = [AssistStatus.Queued, AssistStatus.Running];

    /// <inheritdoc />
    public Task<AssistRun?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.LyricAssists.FirstOrDefaultAsync(assist => assist.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssistRun>> ListForSheetAsync(Guid sheetId, int take, CancellationToken cancellationToken) =>
        await context.LyricAssists.Where(assist => assist.SheetId == sheetId).OrderByDescending(assist => assist.CreatedAt).Take(take).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssistRun>> ActiveAsync(CancellationToken cancellationToken) =>
        await context.LyricAssists.Where(assist => Active.Contains(assist.Status)).OrderBy(assist => assist.CreatedAt).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(AssistRun assist, CancellationToken cancellationToken)
    {
        context.LyricAssists.Add(assist);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> PruneAsync(Guid sheetId, int keep, CancellationToken cancellationToken)
    {
        var old = await context.LyricAssists.Where(assist => assist.SheetId == sheetId).OrderByDescending(assist => assist.CreatedAt).Skip(keep)
            .Select(assist => assist.Id).ToListAsync(cancellationToken);
        var withOpen = await context.LyricSuggestions.Where(s => old.Contains(s.AssistId) && s.Status == SuggestionStatus.Open)
            .Select(s => s.AssistId).Distinct().ToListAsync(cancellationToken);
        var doomed = old.Except(withOpen).ToList();
        return await context.LyricAssists.Where(assist => doomed.Contains(assist.Id) && !Active.Contains(assist.Status)).ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddEventAsync(AssistEvent assistEvent, CancellationToken cancellationToken)
    {
        context.LyricAssistEvents.Add(assistEvent);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssistEvent>> EventsAsync(Guid assistId, int after, CancellationToken cancellationToken) =>
        await context.LyricAssistEvents.AsNoTracking().Where(e => e.AssistId == assistId && e.Seq > after).OrderBy(e => e.Seq).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<int> LastSeqAsync(Guid assistId, CancellationToken cancellationToken) =>
        await context.LyricAssistEvents.Where(e => e.AssistId == assistId).MaxAsync(e => (int?)e.Seq, cancellationToken) ?? 0;

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);

    /// <inheritdoc />
    public Task ReloadAsync(AssistRun assist, CancellationToken cancellationToken) => context.Entry(assist).ReloadAsync(cancellationToken);
}

/// <summary>Persistence of lyric suggestions (feature 023).</summary>
public interface ILyricSuggestionRepository
{
    /// <summary>Suggestions of a sheet (tracked) in text order (titles first, then line and offset); optionally filtered.</summary>
    /// <param name="sheetId">Sheet id.</param>
    /// <param name="status">Status filter.</param>
    /// <param name="category">Category filter.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The suggestions.</returns>
    Task<IReadOnlyList<LyricSuggestion>> ForSheetAsync(Guid sheetId, SuggestionStatus? status, SuggestionCategory? category, CancellationToken cancellationToken);

    /// <summary>Suggestions of an assist in text order.</summary>
    /// <param name="assistId">Assist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The suggestions.</returns>
    Task<IReadOnlyList<LyricSuggestion>> ForAssistAsync(Guid assistId, CancellationToken cancellationToken);

    /// <summary>Suggestions of a sheet by id (tracked); unknown ids are missing from the result.</summary>
    /// <param name="sheetId">Sheet id.</param>
    /// <param name="ids">Ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The suggestions found.</returns>
    Task<IReadOnlyList<LyricSuggestion>> FindManyAsync(Guid sheetId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Number of open suggestions of a sheet.</summary>
    /// <param name="sheetId">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The count.</returns>
    Task<int> CountOpenAsync(Guid sheetId, CancellationToken cancellationToken);

    /// <summary>Adds suggestions (saved with the next save).</summary>
    /// <param name="suggestions">The suggestions.</param>
    void AddRange(IEnumerable<LyricSuggestion> suggestions);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ILyricSuggestionRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class LyricSuggestionRepository(StemStudioDbContext context) : ILyricSuggestionRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LyricSuggestion>> ForSheetAsync(Guid sheetId, SuggestionStatus? status, SuggestionCategory? category, CancellationToken cancellationToken)
    {
        var query = context.LyricSuggestions.Where(s => s.SheetId == sheetId);
        query = status is null ? query : query.Where(s => s.Status == status);
        query = category is null ? query : query.Where(s => s.Category == category);
        return InTextOrder(await query.ToListAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LyricSuggestion>> ForAssistAsync(Guid assistId, CancellationToken cancellationToken) =>
        InTextOrder(await context.LyricSuggestions.Where(s => s.AssistId == assistId).ToListAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<IReadOnlyList<LyricSuggestion>> FindManyAsync(Guid sheetId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await context.LyricSuggestions.Where(s => s.SheetId == sheetId && ids.Contains(s.Id)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> CountOpenAsync(Guid sheetId, CancellationToken cancellationToken) =>
        context.LyricSuggestions.CountAsync(s => s.SheetId == sheetId && s.Status == SuggestionStatus.Open, cancellationToken);

    /// <inheritdoc />
    public void AddRange(IEnumerable<LyricSuggestion> suggestions) => context.LyricSuggestions.AddRange(suggestions);

    // Titles first, then by line and offset (the anchor is a JSON column, so the order is applied in memory).
    private static List<LyricSuggestion> InTextOrder(List<LyricSuggestion> suggestions) =>
        [.. suggestions.OrderBy(s => s.Anchor.Line).ThenBy(s => s.Anchor.Start).ThenBy(s => s.CreatedAt).ThenBy(s => s.Id)];

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}

/// <summary>Persistence of sheet revisions (feature 023, the last 50 per sheet).</summary>
public interface ILyricsRevisionRepository
{
    /// <summary>One revision.</summary>
    /// <param name="sheetId">Sheet id.</param>
    /// <param name="revision">Revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The revision or null.</returns>
    Task<LyricsSheetRevision?> FindAsync(Guid sheetId, int revision, CancellationToken cancellationToken);

    /// <summary>The stored revisions, newest first.</summary>
    /// <param name="sheetId">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The revisions.</returns>
    Task<IReadOnlyList<LyricsSheetRevision>> ListAsync(Guid sheetId, CancellationToken cancellationToken);

    /// <summary>Adds a revision unless it is stored, drops revisions beyond the retention (saved with the next save).</summary>
    /// <param name="revision">The revision.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(LyricsSheetRevision revision, CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ILyricsRevisionRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class LyricsRevisionRepository(StemStudioDbContext context) : ILyricsRevisionRepository
{
    /// <inheritdoc />
    public Task<LyricsSheetRevision?> FindAsync(Guid sheetId, int revision, CancellationToken cancellationToken) =>
        context.LyricsSheetRevisions.FirstOrDefaultAsync(r => r.SheetId == sheetId && r.Revision == revision, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LyricsSheetRevision>> ListAsync(Guid sheetId, CancellationToken cancellationToken) =>
        await context.LyricsSheetRevisions.AsNoTracking().Where(r => r.SheetId == sheetId).OrderByDescending(r => r.Revision).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(LyricsSheetRevision revision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (await context.LyricsSheetRevisions.FindAsync([revision.SheetId, revision.Revision], cancellationToken) is null)
        {
            context.LyricsSheetRevisions.Add(revision);
        }

        var oldest = revision.Revision - LyricsSheetRevision.Retention;
        context.LyricsSheetRevisions.RemoveRange(await context.LyricsSheetRevisions.Where(r => r.SheetId == revision.SheetId && r.Revision <= oldest).ToListAsync(cancellationToken));
    }
}

/// <summary>Persistence of the personal word list and the assistant settings (feature 023).</summary>
public interface ILyricWordRepository
{
    /// <summary>Words of a language, alphabetical (ordinal).</summary>
    /// <param name="language">Language.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The words.</returns>
    Task<IReadOnlyList<LyricWord>> ListAsync(AssistLanguage language, CancellationToken cancellationToken);

    /// <summary>One word (tracked).</summary>
    /// <param name="id">Word id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The word or null.</returns>
    Task<LyricWord?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Number of words of a language.</summary>
    /// <param name="language">Language.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The count.</returns>
    Task<int> CountAsync(AssistLanguage language, CancellationToken cancellationToken);

    /// <summary>Inserts a word.</summary>
    /// <param name="word">The word.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">The word is already in the list.</exception>
    Task AddAsync(LyricWord word, CancellationToken cancellationToken);

    /// <summary>Deletes a word.</summary>
    /// <param name="word">The word.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(LyricWord word, CancellationToken cancellationToken);

    /// <summary>The settings row (created when missing; tracked).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The settings.</returns>
    Task<LyricAssistSettings> SettingsAsync(CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ILyricWordRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class LyricWordRepository(StemStudioDbContext context) : ILyricWordRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<LyricWord>> ListAsync(AssistLanguage language, CancellationToken cancellationToken)
    {
        var words = await context.LyricWords.AsNoTracking().Where(word => word.Language == language).ToListAsync(cancellationToken);
        return [.. words.OrderBy(word => word.Word, StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public Task<LyricWord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.LyricWords.FirstOrDefaultAsync(word => word.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountAsync(AssistLanguage language, CancellationToken cancellationToken) =>
        context.LyricWords.CountAsync(word => word.Language == language, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(LyricWord word, CancellationToken cancellationToken)
    {
        context.LyricWords.Add(word);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(LyricWord word, CancellationToken cancellationToken)
    {
        context.LyricWords.Remove(word);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LyricAssistSettings> SettingsAsync(CancellationToken cancellationToken)
    {
        if (await context.LyricAssistSettings.FirstOrDefaultAsync(cancellationToken) is { } settings)
        {
            return settings;
        }

        var created = new LyricAssistSettings();
        context.LyricAssistSettings.Add(created);
        return created;
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}

/// <summary>Reads the 006 track lyrics of the library for the corpus index (feature 023, research R-07).</summary>
public interface ILyricsCorpusSource
{
    /// <summary>A cheap version stamp: number of lyrics rows and their newest change.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The stamp.</returns>
    Task<(int Count, DateTime? Newest)> StampAsync(CancellationToken cancellationToken);

    /// <summary>The lyric lines of every track that has text.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics.</returns>
    Task<IReadOnlyList<CorpusLyrics>> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ILyricsCorpusSource"/>.</summary>
/// <param name="context">The context.</param>
public sealed class LyricsCorpusSource(StemStudioDbContext context) : ILyricsCorpusSource
{
    /// <inheritdoc />
    public async Task<(int Count, DateTime? Newest)> StampAsync(CancellationToken cancellationToken)
    {
        var lyrics = context.Set<Domain.Lyrics.TrackLyrics>().AsNoTracking();
        return (await lyrics.CountAsync(cancellationToken), await lyrics.MaxAsync(row => (DateTime?)row.UpdatedAt, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CorpusLyrics>> LoadAsync(CancellationToken cancellationToken)
    {
        var rows = await context.Tracks.AsNoTracking().Where(track => track.Lyrics != null && track.Lyrics.Document != null)
            .Select(track => new { track.Id, track.Title, track.Lyrics!.Document }).ToListAsync(cancellationToken);
        return [.. rows.Where(row => row.Document is { Instrumental: false })
            .Select(row => new CorpusLyrics(new CorpusTrack(row.Id, row.Title), row.Document!.Lines.Select(line => line.Text).ToList()))];
    }
}
