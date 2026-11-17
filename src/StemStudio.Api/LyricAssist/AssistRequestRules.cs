using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>Field rules of <c>CreateLyricAssistRequest</c> (data-model 3) and the defaults per kind.</summary>
public static class AssistRequestRules
{
    private static readonly SuggestionCategory[] CheckCategories = [SuggestionCategory.Spelling, SuggestionCategory.Grammar, SuggestionCategory.Flow, SuggestionCategory.Licence];
    private static readonly SuggestionCategory[] DeviceCategories = [SuggestionCategory.Device, SuggestionCategory.Licence];

    /// <summary>The field errors of a request on a sheet.</summary>
    /// <param name="request">The request.</param>
    /// <param name="sheet">The sheet.</param>
    /// <returns>The errors; empty when valid.</returns>
    public static IReadOnlyList<DomainError> Validate(CreateLyricAssistRequest request, LyricsSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sheet);
        var kind = LyricAssistMapper.Map<AssistKind>(request.Kind);
        var errors = new List<DomainError>();
        Check(errors, CategoriesValid(kind, request.Categories), "categories", "These categories do not belong to this kind of help.");
        Check(errors, RangeValid(request, SheetText.Lines(sheet.Text).Length), "lineFrom", "Choose lines inside the sheet, the first not after the last.");
        Check(errors, kind != AssistKind.Alternatives || (request.LineFrom is { } from && from == request.LineTo), "lineFrom", "Alternatives need exactly one line.");
        Check(errors, CountValid(kind, request.Count), "count", "The count is outside the range of this kind of help.");
        Check(errors, InstructionValid(kind, request.Instruction), "instruction", "An instruction is at most 200 characters, for devices and writing help only.");
        Check(errors, request.AgentRunId is null || request.Origin == AssistOriginDto.Agent, "agentRunId", "An agent run id belongs only to an assist the agent starts.");
        return errors;
    }

    /// <summary>The domain request with the defaults of its kind.</summary>
    /// <param name="request">A valid request.</param>
    /// <param name="sheet">The sheet.</param>
    /// <returns>The domain request.</returns>
    public static AssistRequest ToDomain(CreateLyricAssistRequest request, LyricsSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sheet);
        var kind = LyricAssistMapper.Map<AssistKind>(request.Kind);
        var categories = request.Categories is { Count: > 0 } requested ? requested.Select(c => LyricAssistMapper.Map<SuggestionCategory>(c)).Distinct().ToList() : Defaults(kind);
        var lineTo = kind == AssistKind.Continue ? request.LineTo ?? SheetText.Lines(sheet.Text).Length : request.LineTo;
        return new AssistRequest
        {
            Kind = kind,
            Categories = categories,
            LineFrom = request.LineFrom,
            LineTo = lineTo,
            Count = request.Count ?? DefaultCount(kind),
            Instruction = string.IsNullOrWhiteSpace(request.Instruction) ? null : request.Instruction.Trim(),
            Origin = request.Origin == AssistOriginDto.Agent ? AssistOrigin.Agent : AssistOrigin.Editor,
            AgentRunId = request.AgentRunId,
        };
    }

    private static bool CategoriesValid(AssistKind kind, IReadOnlyList<SuggestionCategoryDto>? categories)
    {
        if (categories is null || categories.Count == 0)
        {
            return true;
        }

        var allowed = kind switch
        {
            AssistKind.Check => CheckCategories,
            AssistKind.Devices => DeviceCategories,
            _ => [],
        };
        return categories.Count <= LyricAssistLimits.MaxCategories && categories.All(c => allowed.Contains(LyricAssistMapper.Map<SuggestionCategory>(c)));
    }

    private static bool RangeValid(CreateLyricAssistRequest request, int lines) =>
        InSheet(request.LineFrom, lines) && InSheet(request.LineTo, lines) && (request.LineFrom is null || request.LineTo is null || request.LineFrom <= request.LineTo);

    private static bool InSheet(int? line, int lines) => line is null || (line >= 1 && line <= lines);

    private static bool CountValid(AssistKind kind, int? count) => count is null || (MaxCount(kind) is { } max && count >= LyricAssistLimits.MinCount && count <= max);

    private static bool InstructionValid(AssistKind kind, string? instruction) =>
        string.IsNullOrWhiteSpace(instruction) || (kind != AssistKind.Check && instruction.Trim().Length <= LyricAssistLimits.InstructionMaxLength);

    private static int? MaxCount(AssistKind kind) => kind switch
    {
        AssistKind.Alternatives => LyricAssistLimits.MaxAlternatives,
        AssistKind.Continue => LyricAssistLimits.MaxContinuationLines,
        AssistKind.Titles => LyricAssistLimits.MaxTitles,
        _ => null,
    };

    private static int? DefaultCount(AssistKind kind) => kind switch
    {
        AssistKind.Alternatives => LyricAssistLimits.DefaultAlternatives,
        AssistKind.Continue => LyricAssistLimits.DefaultContinuationLines,
        AssistKind.Titles => LyricAssistLimits.DefaultTitles,
        _ => null,
    };

    private static List<SuggestionCategory> Defaults(AssistKind kind) => kind switch
    {
        AssistKind.Check => [.. CheckCategories],
        AssistKind.Devices => [.. DeviceCategories],
        AssistKind.Alternatives => [SuggestionCategory.Alternative],
        AssistKind.Continue => [SuggestionCategory.Continuation],
        _ => [SuggestionCategory.Title],
    };

    private static void Check(List<DomainError> errors, bool valid, string field, string message)
    {
        if (!valid)
        {
            errors.Add(new DomainError(field, message));
        }
    }
}
