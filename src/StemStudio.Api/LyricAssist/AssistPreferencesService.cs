using StemStudio.Api.Tracks;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// What the user sets for the checker (FR-010 - FR-012, FR-017): intentional marks per sheet, sensitivity and rhythm target per sheet,
/// and the personal word list per language. None of these change the text or the revision.
/// </summary>
/// <param name="stores">Stores.</param>
/// <param name="changes">Sheet assist info.</param>
/// <param name="clock">Clock.</param>
public sealed class AssistPreferencesService(LyricAssistStores stores, LyricsSheetChanges changes, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>The marks of a sheet with whether each occurs.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> ListMarksAsync(Guid lyricsId, CancellationToken cancellationToken) =>
        await stores.Sheets.FindAsync(lyricsId, cancellationToken) is { } sheet
            ? TypedResults.Ok(sheet.IntentionalMarks.Select(mark => MarkMapper.ToDto(mark, sheet.Text)).ToList())
            : SheetProblems.NotFound(lyricsId);

    /// <summary>Adds a mark (an identical one is returned).</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="input">The mark.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201, 400, 404 or 409.</returns>
    public async Task<IResult> CreateMarkAsync(Guid lyricsId, IntentionalMarkInput? input, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet)
        {
            return SheetProblems.NotFound(lyricsId);
        }

        IntentionalMark mark;
        try
        {
            mark = IntentionalMark.Create(Guid.NewGuid(), new MarkContent(LyricAssistMapper.Map<IntentionalScope>(input?.Scope ?? IntentionalScopeDto.Word), input?.Text ?? string.Empty, input?.Note), Now);
        }
        catch (DomainValidationException invalid)
        {
            return ApiProblems.Validation(invalid.Errors, string.Empty);
        }

        if (sheet.AddMark(mark) is not { } stored)
        {
            return LyricAssistProblems.MarkLimit();
        }

        await stores.Sheets.SaveAsync(cancellationToken);
        return TypedResults.Created(Contracts.ApiRoutes.LyricsIntentionalMark(sheet.Id, stored.Id), MarkMapper.ToDto(stored, sheet.Text));
    }

    /// <summary>Removes a mark.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="markId">Mark id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteMarkAsync(Guid lyricsId, Guid markId, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet || !sheet.RemoveMark(markId))
        {
            return ApiProblems.NotFound($"Mark {markId} does not exist on these lyrics.");
        }

        await stores.Sheets.SaveAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Sets sensitivity and/or rhythm target.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="update">The update (absent rhythm target keeps, null clears).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400 or 404.</returns>
    public async Task<IResult> UpdateSheetSettingsAsync(Guid lyricsId, SheetAssistSettingsUpdate? update, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet)
        {
            return SheetProblems.NotFound(lyricsId);
        }

        if (LyricAssistMapper.TargetProblem(update?.RhythmTarget, out var target) is { } problem)
        {
            return problem;
        }

        sheet.SetAssistSettings(update?.Sensitivity is { } sensitivity ? LyricAssistMapper.Map<Sensitivity>(sensitivity) : null, update?.RhythmTargetSpecified == true, target);
        await stores.Sheets.SaveAsync(cancellationToken);
        return TypedResults.Ok(await changes.InfoAsync(sheet, cancellationToken));
    }

    /// <summary>The words of a language.</summary>
    /// <param name="language">Language (required).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 400.</returns>
    public async Task<IResult> ListWordsAsync(AssistLanguageDto? language, CancellationToken cancellationToken) => language is { } lang
        ? TypedResults.Ok((await stores.Words.ListAsync(LyricAssistMapper.ToDomain(lang), cancellationToken)).Select(ToDto).ToList())
        : ApiProblems.Validation("language", "Choose nl or en.");

    /// <summary>Adds a word (an existing one is returned).</summary>
    /// <param name="input">The word.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201, 400 or 409.</returns>
    public async Task<IResult> AddWordAsync(LyricWordInput? input, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return ApiProblems.Validation("word", "Send the language and the word.");
        }

        var language = LyricAssistMapper.ToDomain(input.Language);
        LyricWord word;
        try
        {
            word = LyricWord.Create(Guid.NewGuid(), language, input.Word, Now);
        }
        catch (DomainValidationException invalid)
        {
            return ApiProblems.Validation(invalid.Errors, string.Empty);
        }

        return await ExistingAsync(word, cancellationToken) ?? await InsertAsync(word, cancellationToken);
    }

    /// <summary>Removes a word.</summary>
    /// <param name="wordId">Word id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteWordAsync(Guid wordId, CancellationToken cancellationToken)
    {
        if (await stores.Words.FindAsync(wordId, cancellationToken) is not { } word)
        {
            return ApiProblems.NotFound($"Word {wordId} is not in your word list.");
        }

        await stores.Words.RemoveAsync(word, cancellationToken);
        return TypedResults.NoContent();
    }

    private async Task<IResult?> ExistingAsync(LyricWord word, CancellationToken cancellationToken) =>
        (await stores.Words.ListAsync(word.Language, cancellationToken)).FirstOrDefault(existing => existing.Word == word.Word) is { } existing
            ? TypedResults.Created(Contracts.ApiRoutes.LyricAssistWord(existing.Id), ToDto(existing))
            : null;

    private async Task<IResult> InsertAsync(LyricWord word, CancellationToken cancellationToken)
    {
        if (await stores.Words.CountAsync(word.Language, cancellationToken) >= LyricWord.MaxPerLanguage)
        {
            return LyricAssistProblems.WordLimit();
        }

        try
        {
            await stores.Words.AddAsync(word, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            return await ExistingAsync(word, cancellationToken) ?? LyricAssistProblems.WordLimit();
        }

        return TypedResults.Created(Contracts.ApiRoutes.LyricAssistWord(word.Id), ToDto(word));
    }

    private static LyricWordDto ToDto(LyricWord word) => new()
    {
        WordId = word.Id, Language = LyricAssistMapper.ToDto(word.Language), Word = word.Word, CreatedAt = TrackMapper.Utc(word.CreatedAt),
    };
}
