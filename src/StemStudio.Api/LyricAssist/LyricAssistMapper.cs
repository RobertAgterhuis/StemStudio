using StemStudio.Api.Styles;
using StemStudio.Api.Tracks;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>Maps the 023 domain model to the contract DTOs (enum members share their names, except the languages).</summary>
public static class LyricAssistMapper
{
    /// <summary>Contract language of a domain language.</summary>
    /// <param name="language">Language.</param>
    /// <returns>The DTO.</returns>
    public static AssistLanguageDto ToDto(AssistLanguage language) => language == AssistLanguage.Dutch ? AssistLanguageDto.Nl : AssistLanguageDto.En;

    /// <summary>Domain language of a contract language.</summary>
    /// <param name="language">Language.</param>
    /// <returns>The domain value.</returns>
    public static AssistLanguage ToDomain(AssistLanguageDto language) => language == AssistLanguageDto.Nl ? AssistLanguage.Dutch : AssistLanguage.English;

    /// <summary>Maps an enum by member name.</summary>
    /// <typeparam name="T">Target enum.</typeparam>
    /// <param name="value">Value.</param>
    /// <returns>The mapped value.</returns>
    public static T Map<T>(Enum value)
        where T : struct, Enum => StyleMapper.Map<T>(value);

    /// <summary>The assistant state of a sheet.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="openSuggestions">Open suggestions of the sheet.</param>
    /// <returns>The DTO.</returns>
    public static SheetAssistInfoDto Info(LyricsSheet sheet, int openSuggestions)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return new SheetAssistInfoDto
        {
            Sensitivity = Map<SensitivityDto>(sheet.Sensitivity),
            RhythmTarget = sheet.RhythmTarget is { } target ? ToDto(target) : null,
            OpenSuggestions = openSuggestions,
            AssistedLines = sheet.AssistedLineNumbers().Select(line => new AssistedLineDto { Line = line.Line, SuggestionId = line.SuggestionId }).ToList(),
            IntentionalMarkCount = sheet.IntentionalMarks.Count,
            CanUndo = sheet.CanUndo,
            OwnWorkOverride = sheet.OwnWorkOverride is { } own ? ToDto(own) : null,
        };
    }

    /// <summary>The rhythm target DTO.</summary>
    /// <param name="target">Target.</param>
    /// <returns>The DTO.</returns>
    public static RhythmTargetDto ToDto(RhythmTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new RhythmTargetDto { Bpm = target.Bpm, BeatsPerBar = target.BeatsPerBar, BarsPerLine = target.BarsPerLine, SyllablesPerBeat = target.SyllablesPerBeat };
    }

    /// <summary>The domain rhythm target.</summary>
    /// <param name="target">DTO.</param>
    /// <returns>The target.</returns>
    public static RhythmTarget ToDomain(RhythmTargetDto target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new RhythmTarget(target.Bpm, target.BeatsPerBar, target.BarsPerLine, target.SyllablesPerBeat);
    }

    /// <summary>The own-work override DTO.</summary>
    /// <param name="own">Override.</param>
    /// <returns>The DTO.</returns>
    public static OwnWorkOverrideDto ToDto(OwnWorkOverride own)
    {
        ArgumentNullException.ThrowIfNull(own);
        return new OwnWorkOverrideDto
        {
            StatementVersion = own.StatementVersion, ConfirmedBy = own.ConfirmedBy, ConfirmedAt = TrackMapper.Utc(own.ConfirmedAt),
            MatchedTrackId = own.MatchedTrackId, MatchedTrackTitle = own.MatchedTrackTitle,
        };
    }

    /// <summary>The suggestion DTO.</summary>
    /// <param name="suggestion">Suggestion.</param>
    /// <returns>The DTO.</returns>
    public static LyricSuggestionDto ToDto(LyricSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        return new LyricSuggestionDto
        {
            SuggestionId = suggestion.Id, AssistId = suggestion.AssistId, Category = Map<SuggestionCategoryDto>(suggestion.Category),
            Type = Map<SuggestionTypeDto>(suggestion.Type), Severity = Map<SuggestionSeverityDto>(suggestion.Severity), Anchor = ToDto(suggestion.Anchor),
            Operation = Map<SuggestionOperationDto>(suggestion.Operation), Proposal = suggestion.Proposal, GroupId = suggestion.GroupId,
            Explanation = suggestion.Explanation, Source = Map<SuggestionSourceDto>(suggestion.Source), Model = suggestion.Model,
            SyllablesBefore = suggestion.SyllablesBefore, SyllablesAfter = suggestion.SyllablesAfter,
            RhymeWith = suggestion.RhymeWith is { } rhyme ? new RhymePartnerDto { Line = rhyme.Line, Word = rhyme.Word } : null,
            Status = Map<SuggestionStatusDto>(suggestion.Status), DecidedAt = suggestion.DecidedAt is { } decided ? TrackMapper.Utc(decided) : null,
        };
    }

    /// <summary>The anchor DTO.</summary>
    /// <param name="anchor">Anchor.</param>
    /// <returns>The DTO.</returns>
    public static AnchorDto ToDto(Anchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        return new AnchorDto { Line = anchor.Line, Start = anchor.Start, Length = anchor.Length, Original = anchor.Original };
    }

    /// <summary>The domain anchor.</summary>
    /// <param name="anchor">DTO.</param>
    /// <returns>The anchor.</returns>
    public static Anchor ToDomain(AnchorDto anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        return new Anchor(anchor.Line, anchor.Start, anchor.Length, anchor.Original);
    }

    /// <summary>The assist summary DTO.</summary>
    /// <param name="assist">Assist.</param>
    /// <returns>The DTO.</returns>
    public static LyricAssistSummaryDto ToSummary(AssistRun assist) => Fill(new LyricAssistSummaryDto
    {
        AssistId = Guid.Empty, LyricsId = Guid.Empty, SheetRevision = 0, Kind = default, Status = default, Language = default, Sensitivity = default, Layers = [],
        UsingFallback = false, BlocksTotal = 0, BlocksDone = 0, BlocksSkipped = 0, Counts = CategoryCountsDto.None, Discarded = DiscardedCountsDto.None,
        Origin = default, CreatedAt = default,
    }, assist);

    /// <summary>The assist DTO with its suggestions.</summary>
    /// <param name="assist">Assist.</param>
    /// <param name="suggestions">Its suggestions.</param>
    /// <returns>The DTO.</returns>
    public static LyricAssistDto ToDto(AssistRun assist, IEnumerable<LyricSuggestion> suggestions) => Fill(new LyricAssistDto
    {
        AssistId = Guid.Empty, LyricsId = Guid.Empty, SheetRevision = 0, Kind = default, Status = default, Language = default, Sensitivity = default, Layers = [],
        UsingFallback = false, BlocksTotal = 0, BlocksDone = 0, BlocksSkipped = 0, Counts = CategoryCountsDto.None, Discarded = DiscardedCountsDto.None,
        Origin = default, CreatedAt = default, Suggestions = suggestions.Select(ToDto).ToList(),
    }, assist);

    /// <summary>The layer DTO.</summary>
    /// <param name="layer">Layer.</param>
    /// <returns>The DTO.</returns>
    public static LayerInfoDto ToDto(AssistLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return new LayerInfoDto { Layer = Map<SuggestionSourceDto>(layer.Layer), Name = layer.Name, Available = layer.Available, Reason = layer.Reason, Fix = layer.Fix };
    }

    /// <summary>The counts DTO.</summary>
    /// <param name="counts">Counts.</param>
    /// <returns>The DTO.</returns>
    public static CategoryCountsDto ToDto(CategoryCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return new CategoryCountsDto
        {
            Spelling = counts.Spelling, Grammar = counts.Grammar, Flow = counts.Flow, Licence = counts.Licence, Device = counts.Device,
            Alternative = counts.Alternative, Continuation = counts.Continuation, Title = counts.Title,
        };
    }

    /// <summary>The discarded DTO.</summary>
    /// <param name="discarded">Counts.</param>
    /// <returns>The DTO.</returns>
    public static DiscardedCountsDto ToDto(DiscardedCounts discarded)
    {
        ArgumentNullException.ThrowIfNull(discarded);
        return new DiscardedCountsDto
        {
            InvalidAnchor = discarded.InvalidAnchor, InvalidSchema = discarded.InvalidSchema, FailedVerification = discarded.FailedVerification,
            Filtered = discarded.Filtered, Verbatim = discarded.Verbatim,
        };
    }

    /// <summary>The domain discarded counts.</summary>
    /// <param name="discarded">DTO.</param>
    /// <returns>The counts.</returns>
    public static DiscardedCounts ToDomain(DiscardedCountsDto discarded)
    {
        ArgumentNullException.ThrowIfNull(discarded);
        return new DiscardedCounts
        {
            InvalidAnchor = discarded.InvalidAnchor, InvalidSchema = discarded.InvalidSchema, FailedVerification = discarded.FailedVerification,
            Filtered = discarded.Filtered, Verbatim = discarded.Verbatim,
        };
    }

    private static T Fill<T>(T dto, AssistRun assist)
        where T : LyricAssistSummaryDto
    {
        ArgumentNullException.ThrowIfNull(assist);
        return dto with
        {
            AssistId = assist.Id, LyricsId = assist.SheetId, SheetRevision = assist.SheetRevision, Kind = Map<AssistKindDto>(assist.Kind),
            Status = Map<AssistStatusDto>(assist.Status), Language = ToDto(assist.Language), Sensitivity = Map<SensitivityDto>(assist.Sensitivity),
            Layers = assist.Layers.Select(ToDto).ToList(), Backend = Backend(assist.Backend), Model = assist.Model, UsingFallback = assist.UsingFallback,
            BlocksTotal = assist.BlocksTotal, BlocksDone = assist.BlocksDone, BlocksSkipped = assist.BlocksSkipped, Counts = ToDto(assist.Counts),
            Discarded = ToDto(assist.Discarded), Origin = Map<AssistOriginDto>(assist.Origin), AgentRunId = assist.AgentRunId,
            Usage = assist.Usage is { } usage ? new TokenUsageDto { InputTokens = usage.InputTokens, OutputTokens = usage.OutputTokens, CachedInputTokens = usage.CachedInputTokens } : null,
            Failure = Failure(assist), EstimatedSeconds = assist.EstimatedSeconds, CreatedAt = TrackMapper.Utc(assist.CreatedAt),
            StartedAt = assist.StartedAt is { } started ? TrackMapper.Utc(started) : null, EndedAt = assist.EndedAt is { } ended ? TrackMapper.Utc(ended) : null,
        };
    }

    private static AssistFailureDto? Failure(AssistRun assist) => assist.FailureCode is { } code
        ? new AssistFailureDto { Code = Map<AssistFailureCodeDto>(code), Message = assist.FailureMessage ?? code.ToString(), Fix = assist.FailureFix }
        : null;

    private static AgentBackendDto? Backend(string? backend) => backend switch
    {
        "ollama" => AgentBackendDto.Ollama,
        "claude" => AgentBackendDto.Claude,
        "fake" => AgentBackendDto.Fake,
        _ => null,
    };
}
