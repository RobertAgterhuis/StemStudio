using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>One model item on its way to a suggestion: anchor, operation and verification (used by <see cref="SuggestionValidator"/>).</summary>
internal sealed class SuggestionCandidate(ValidationContext context, ModelSuggestionItem item)
{
    private readonly LyricText _text = new(context.Job.Text);
    private readonly SuggestionCategoryDto _category = LyricTypes.Category(item.Type);
    private AnchorDto? _anchor;
    private SuggestionOperationDto _operation;
    private string? _proposal;
    private int? _before;
    private int? _after;
    private RhymePartnerDto? _rhyme;

    private AssistLanguageDto Language => context.Job.Language;

    /// <summary>Runs every check; null when the item is valid.</summary>
    /// <returns>The reason to drop it, or null.</returns>
    public DiscardReason? Check()
    {
        if (!LyricTypes.Allowed(context.Job.Kind, context.Job.Categories).Contains(_category))
        {
            return DiscardReason.InvalidSchema;
        }

        _anchor = Anchor();
        if (_anchor is null)
        {
            return DiscardReason.InvalidAnchor;
        }

        return Shape() ?? Verify() ?? Verbatim();
    }

    /// <summary>True when the other suggestion has the same anchor, operation and proposal (merged).</summary>
    /// <param name="other">An accepted suggestion.</param>
    /// <returns>True for a duplicate.</returns>
    public bool Duplicates(AssistantSuggestion other) =>
        other.Anchor.Line == _anchor!.Line && other.Anchor.Start == _anchor.Start && other.Anchor.Length == _anchor.Length
        && other.Operation == _operation && other.Proposal == _proposal;

    /// <summary>The validated suggestion.</summary>
    /// <returns>The suggestion.</returns>
    public AssistantSuggestion ToSuggestion() => new()
    {
        Category = _category,
        Type = item.Type,
        Severity = LyricTypes.IsCorrection(_category) ? item.Severity : SuggestionSeverityDto.Idea,
        Anchor = _anchor!,
        Operation = _operation,
        Proposal = _proposal,
        GroupId = LyricTypes.IsWriting(context.Job.Kind) ? context.GroupId : null,
        Explanation = item.Explanation.Trim(),
        SyllablesBefore = _before,
        SyllablesAfter = _after,
        RhymeWith = _rhyme,
    };

    private AnchorDto? Anchor() => context.Job.Kind switch
    {
        AssistKindDto.Titles => item.Line == 0 && item.Original == context.Job.Title
            ? new AnchorDto { Line = 0, Start = 0, Length = context.Job.Title.Length, Original = context.Job.Title }
            : null,
        AssistKindDto.Continue => ContinuationAnchor(),
        _ => InLine(),
    };

    private AnchorDto? ContinuationAnchor()
    {
        var line = context.Job.LineTo ?? context.Block.LastLine;
        return item.Line == line && item.Original.Length == 0 && _text.Line(line) is { } text && InBlock(line)
            ? new AnchorDto { Line = line, Start = text.Length, Length = 0, Original = string.Empty }
            : null;
    }

    /// <summary>The original on its line: exact first, then once more ignoring case (the model's casing may differ).</summary>
    private AnchorDto? InLine()
    {
        if (!InBlock(item.Line) || !InRange(item.Line) || !_text.IsReviewable(item.Line) || item.Original.Trim().Length == 0)
        {
            return null;
        }

        var line = _text.Line(item.Line)!;
        var start = line.IndexOf(item.Original, StringComparison.Ordinal);
        start = start >= 0 ? start : line.IndexOf(item.Original, StringComparison.OrdinalIgnoreCase);
        return start < 0 ? null : new AnchorDto { Line = item.Line, Start = start, Length = item.Original.Length, Original = line.Substring(start, item.Original.Length) };
    }

    private bool InBlock(int line) => line >= context.Block.FirstLine && line <= context.Block.LastLine;

    private bool InRange(int line) =>
        context.Job.Kind == AssistKindDto.Alternatives
            ? line == (context.Job.LineFrom ?? line)
            : line >= (context.Job.LineFrom ?? line) && line <= (context.Job.LineTo ?? line);

    /// <summary>Operation and proposal per category, and the replacement limits.</summary>
    private DiscardReason? Shape()
    {
        var proposal = item.Proposal?.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (_category == SuggestionCategoryDto.Licence || (_category == SuggestionCategoryDto.Device && string.IsNullOrEmpty(proposal)))
        {
            _operation = SuggestionOperationDto.None;
            return null;
        }

        if (string.IsNullOrEmpty(proposal) || proposal == _anchor!.Original)
        {
            return DiscardReason.InvalidSchema;
        }

        _proposal = proposal;
        _operation = Operation();
        return WithinLimits(proposal) ? null : DiscardReason.InvalidSchema;
    }

    private SuggestionOperationDto Operation() => _category switch
    {
        SuggestionCategoryDto.Continuation => SuggestionOperationDto.InsertAfter,
        SuggestionCategoryDto.Title => SuggestionOperationDto.SetTitle,
        _ => SuggestionOperationDto.Replace,
    };

    private bool WithinLimits(string proposal) => _category switch
    {
        SuggestionCategoryDto.Continuation => ContinuationLines(proposal),
        SuggestionCategoryDto.Title => proposal.Length <= SuggestionValidator.MaxTitleLength && !proposal.Contains('\n', StringComparison.Ordinal),
        _ => !proposal.Contains('\n', StringComparison.Ordinal) && (!LyricTypes.IsCorrection(_category) || SmallChange(proposal)),
    };

    private bool ContinuationLines(string proposal)
    {
        var lines = proposal.Split('\n');
        var wanted = Math.Clamp(context.Job.Count ?? LyricAssistLimits.DefaultContinuationLines, 1, LyricAssistLimits.MaxContinuationLines);
        return lines.Length <= wanted && lines.All(line => line.Trim().Length > 0 && !LyricText.IsMarker(line));
    }

    /// <summary>A correction may change at most 60 % of the line (edit distance of original to proposal over the line length).</summary>
    private bool SmallChange(string proposal) =>
        EditDistance.Of(_anchor!.Original, proposal) <= SuggestionValidator.MaxCorrectionShare * Math.Max(1, _text.Line(_anchor.Line)!.Trim().Length);

    /// <summary>Computes syllables and checks rhyme and syllable-fit claims (FR-014).</summary>
    private DiscardReason? Verify()
    {
        if (_operation == SuggestionOperationDto.Replace)
        {
            _before = context.Verification.CountSyllables(_text.Line(_anchor!.Line)!, Language);
            _after = context.Verification.CountSyllables(ResultingLine(), Language);
        }

        return item.Type switch
        {
            SuggestionTypeDto.DeviceEndRhyme or SuggestionTypeDto.DeviceInternalRhyme => VerifyRhyme(),
            SuggestionTypeDto.DeviceSyllableFit => VerifySyllableFit(),
            _ => null,
        };
    }

    private DiscardReason? VerifyRhyme()
    {
        if (item.RhymeWithLine is not { } partnerLine || string.IsNullOrWhiteSpace(item.RhymeWord) || _text.Line(partnerLine) is not { } partnerText
            || !LyricText.Words(partnerText).Contains(item.RhymeWord.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return DiscardReason.FailedVerification;
        }

        var partner = item.RhymeWord.Trim();
        var resulting = _proposal is null ? _anchor!.Original : ResultingLine();
        IEnumerable<string> words = item.Type == SuggestionTypeDto.DeviceEndRhyme ? [LyricText.LastWord(resulting) ?? string.Empty] : LyricText.Words(resulting);
        if (!words.Any(word => !word.Equals(partner, StringComparison.OrdinalIgnoreCase) && context.Verification.Rhyme(word, partner, Language) is not null))
        {
            return DiscardReason.FailedVerification;
        }

        _rhyme = new RhymePartnerDto { Line = partnerLine, Word = partner };
        return null;
    }

    private string ResultingLine()
    {
        var line = _text.Line(_anchor!.Line)!;
        return line.Remove(_anchor.Start, _anchor.Length).Insert(_anchor.Start, _proposal!);
    }

    /// <summary>A syllable-fit proposal must bring the line closer to the rhythm target (or, without a target, change the count).</summary>
    private DiscardReason? VerifySyllableFit()
    {
        if (_before is not { } before || _after is not { } after)
        {
            return DiscardReason.FailedVerification;
        }

        var fits = context.Job.RhythmTarget is { } target
            ? Math.Abs(after - Target(target)) < Math.Abs(before - Target(target))
            : after != before;
        return fits ? null : DiscardReason.FailedVerification;
    }

    private static double Target(RhythmTargetDto target) => target.BeatsPerBar * target.BarsPerLine * target.SyllablesPerBeat;

    /// <summary>Generated text with a 7-word run or a full 5+-word line of library lyrics is withheld (FR-023).</summary>
    private DiscardReason? Verbatim() =>
        _proposal is not null && context.Verification.IsVerbatim(_proposal) ? DiscardReason.Verbatim : null;
}
