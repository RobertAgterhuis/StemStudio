using System.Globalization;
using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Rhyme candidates (research R-04): Dutch from the OpenTaal word list (lower-case entries of <c>nl.dic</c>), English from CMUdict.
/// Loaded lazily once.
/// </summary>
/// <param name="dutchDic">Path of <c>nl.dic</c>.</param>
/// <param name="english">CMU lexicon.</param>
public sealed class DictionaryWordList(string dutchDic, CmuPronunciationLexicon english) : IWordList
{
    private const int MaxWordLength = 20;

    private readonly Lazy<IReadOnlyCollection<string>> _dutch = new(() => VendoredDataCache.Get("nl-words:" + dutchDic, () => LoadDutch(dutchDic)));
    private readonly Lazy<IReadOnlyCollection<string>> _english = new(() => english.Words.Where(IsPlain).ToList());

    /// <summary>The default <c>nl.dic</c>.</summary>
    public static string DefaultDutchPath { get; } = Path.Combine(HunspellSpellChecker.DefaultRoot, "nl", "nl.dic");

    /// <inheritdoc />
    public IReadOnlyCollection<string> Words(AssistLanguage language) => language == AssistLanguage.Dutch ? _dutch.Value : _english.Value;

    private static List<string> LoadDutch(string path) => File.ReadLines(path).Skip(1)
        .Select(line => line.Split('/', 2)[0].Trim())
        .Where(IsPlain)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    private static bool IsPlain(string word) => word.Length is > 1 and <= MaxWordLength && word.All(char.IsLower);
}

/// <summary>The ai-agent's <see cref="ILyricVerification"/> over the Domain services (DELTA.md 5 "Ports across lanes").</summary>
/// <param name="phonology">Syllables and rhymes.</param>
/// <param name="corpus">Library lyrics.</param>
public sealed class LyricVerification(LyricPhonology phonology, LyricsCorpusProvider corpus) : ILyricVerification
{
    /// <inheritdoc />
    public int CountSyllables(string line, AssistLanguageDto language) => phonology.LineSyllables(line ?? string.Empty, LyricAssistMapper.ToDomain(language));

    /// <inheritdoc />
    public RhymeKindDto? Rhyme(string word, string other, AssistLanguageDto language) =>
        phonology.Rhyme(word ?? string.Empty, other ?? string.Empty, LyricAssistMapper.ToDomain(language)) switch
        {
            RhymeKind.Perfect => RhymeKindDto.Perfect,
            RhymeKind.Near => RhymeKindDto.Near,
            _ => null,
        };

    /// <inheritdoc />
    public bool IsVerbatim(string generatedText) => VerbatimGuard.IsVerbatim(generatedText, corpus.Current);
}

/// <summary>Syllables, rhymes and the language guess (FR-001, FR-016, FR-017): deterministic, local, nothing stored.</summary>
/// <param name="stores">Stores.</param>
/// <param name="phonology">Syllables.</param>
/// <param name="rhymes">Rhyme finder.</param>
/// <param name="spelling">Spell checker (language guess).</param>
public sealed class LyricLanguageService(LyricAssistStores stores, LyricPhonology phonology, RhymeFinder rhymes, ISpellChecker spelling)
{
    /// <summary>Syllable report of a saved sheet.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 409.</returns>
    public async Task<IResult> SheetSyllablesAsync(Guid lyricsId, CancellationToken cancellationToken)
    {
        if (await stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet)
        {
            return SheetProblems.NotFound(lyricsId);
        }

        return sheet.AssistLanguage is { } language ? TypedResults.Ok(Report(sheet.Text, language, sheet.RhythmTarget)) : LyricAssistProblems.LanguageUnsupported();
    }

    /// <summary>Syllable report of editor text.</summary>
    /// <param name="request">Language, text and optional target.</param>
    /// <returns>200 or 400.</returns>
    public IResult TextSyllables(SyllableRequest? request)
    {
        if (request?.Text is not { Length: > 0 and <= LyricAssistLimits.RawTextMaxLength } text)
        {
            return ApiProblems.Validation("text", "Send 1 to 65,536 characters of lyrics.");
        }

        if (LyricAssistMapper.TargetProblem(request.RhythmTarget, out var target) is { } problem)
        {
            return problem;
        }

        return TypedResults.Ok(Report(LyricsNormaliser.Normalize(text), LyricAssistMapper.ToDomain(request.Language), target));
    }

    /// <summary>Rhymes of a word.</summary>
    /// <param name="word">The word (1-40).</param>
    /// <param name="language">Language.</param>
    /// <param name="limit">Maximum (1-60, default 30).</param>
    /// <returns>200 or 400.</returns>
    public IResult Rhymes(string? word, AssistLanguageDto? language, int? limit)
    {
        var trimmed = word?.Trim() ?? string.Empty;
        if (RhymeProblem(trimmed, language, limit) is { } problem)
        {
            return problem;
        }

        var lang = language!.Value;
        var found = rhymes.Find(trimmed, LyricAssistMapper.ToDomain(lang), limit ?? LyricAssistLimits.DefaultRhymes);
        return TypedResults.Ok(new RhymeResultDto
        {
            Word = trimmed,
            Language = lang,
            Source = lang == AssistLanguageDto.Nl ? "OpenTaal word list, spelling rhyme rules" : "CMU Pronouncing Dictionary",
            Rhymes = found.Select(r => new RhymeDto { Word = r.Word, Kind = r.Kind == RhymeKind.Perfect ? RhymeKindDto.Perfect : RhymeKindDto.Near, Syllables = r.Syllables }).ToList(),
        });
    }

    private static IResult? RhymeProblem(string word, AssistLanguageDto? language, int? limit)
    {
        if (word.Length is 0 or > LyricAssistLimits.WordMaxLength || word.Any(char.IsWhiteSpace))
        {
            return ApiProblems.Validation("word", "Enter one word of 1 to 40 characters.");
        }

        if (language is null)
        {
            return ApiProblems.Validation("language", "Choose nl or en.");
        }

        return limit is < 1 or > LyricAssistLimits.MaxRhymes ? ApiProblems.Validation("limit", "The limit is 1 to 60.") : null;
    }

    /// <summary>Guesses Dutch or English.</summary>
    /// <param name="request">The text.</param>
    /// <returns>200 or 400.</returns>
    public IResult Guess(LanguageGuessRequest? request)
    {
        if (request?.Text is not { Length: > 0 and <= LyricAssistLimits.RawTextMaxLength } text)
        {
            return ApiProblems.Validation("text", "Send 1 to 65,536 characters of lyrics.");
        }

        var (language, confidence) = LanguageGuesser.Guess(LyricsNormaliser.Normalize(text), spelling);
        return TypedResults.Ok(new LanguageGuessDto { Language = language is { } l ? LyricAssistMapper.ToDto(l) : null, Confidence = confidence });
    }

    private SyllableReportDto Report(string text, AssistLanguage language, RhythmTarget? target)
    {
        var report = SyllableReport.Build(text, language, target, phonology);
        return new SyllableReportDto
        {
            Language = LyricAssistMapper.ToDto(language),
            TargetDurationSeconds = report.TargetDurationSeconds,
            WordsPerSecond = report.WordsPerSecond is { } wps ? Math.Round(wps, 3) : null,
            Lines = report.Lines.Select(l => new SyllableLineDto
            {
                Line = l.Line,
                Syllables = l.Syllables,
                IsMarker = l.IsMarker,
                Section = l.Section,
                SectionMedian = l.SectionMedian,
                Target = l.Target,
                Deviates = l.Deviates,
            }).ToList(),
        };
    }
}

/// <summary>Maps the syllable, rhyme and language-guess routes (backend 1.23.0).</summary>
public static class LyricLanguageEndpoints
{
    /// <summary>Maps the routes.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapLyricLanguageEndpoints(this IEndpointRouteBuilder app)
    {
        const string Tag = LyricAssistEndpoints.Tag;
        app.MapGet(Contracts.ApiRoutes.LyricsSheets + "/{lyricsId:guid}/syllables", (Guid lyricsId, LyricLanguageService service, CancellationToken ct) => service.SheetSyllablesAsync(lyricsId, ct))
            .WithName("getSheetSyllables").WithTags(Tag).Produces<SyllableReportDto>().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapPost(Contracts.ApiRoutes.LyricAssistSyllables, (SyllableRequest? request, LyricLanguageService service) => service.TextSyllables(request))
            .WithName("countSyllables").WithTags(Tag).Produces<SyllableReportDto>().ProducesValidationProblem();
        app.MapGet(Contracts.ApiRoutes.LyricAssistRhymes, (string? word, string? language, string? limit, LyricLanguageService service) =>
                Limit(limit) is { Valid: true } parsed ? service.Rhymes(word, Language(language), parsed.Value) : ApiProblems.Validation("limit", "The limit is 1 to 60."))
            .WithName("findRhymes").WithTags(Tag).Produces<RhymeResultDto>().ProducesValidationProblem();
        app.MapPost(Contracts.ApiRoutes.LyricAssistLanguageGuess, (LanguageGuessRequest? request, LyricLanguageService service) => service.Guess(request))
            .WithName("guessLyricsLanguage").WithTags(Tag).Produces<LanguageGuessDto>().ProducesValidationProblem();
        return app;
    }

    private static AssistLanguageDto? Language(string? text) => SuggestionEndpoints.TryParse(text, out AssistLanguageDto language) ? language : null;

    private static (bool Valid, int? Value) Limit(string? text) =>
        text is null ? (true, null) : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? (true, value) : (false, null);
}
