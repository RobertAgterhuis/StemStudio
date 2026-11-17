using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>What one block is validated against.</summary>
/// <param name="Job">The assist (text, kind, range, counts).</param>
/// <param name="Block">The block the output belongs to.</param>
/// <param name="Verification">Syllables, rhymes and the verbatim guard.</param>
/// <param name="GroupId">Group id of the writing-help items of the block (alternatives of one line share it).</param>
public sealed record ValidationContext(LyricAssistJob Job, LyricBlock Block, ILyricVerification Verification, Guid GroupId);

/// <summary>Why an item was dropped (the <see cref="DiscardedCountsDto"/> counters).</summary>
public enum DiscardReason
{
    /// <summary>The anchor does not match the real text (line, original, block).</summary>
    InvalidAnchor,

    /// <summary>The item breaks the schema, the kind's types or the replacement limits.</summary>
    InvalidSchema,

    /// <summary>A rhyme or syllable claim failed the deterministic check (FR-014).</summary>
    FailedVerification,

    /// <summary>The generated text contains library lyrics (FR-023).</summary>
    Verbatim,
}

/// <summary>
/// Validates the parsed model output of one block (pure, research R-05, FR-005/FR-013-FR-015/FR-021/FR-023): every anchor is
/// re-found in the real text, the type must belong to the kind and the requested categories, corrections may change at most
/// 60 % of a line, writing help follows its count and shape, rhyme and syllable-fit claims are verified, syllables are computed
/// (never taken from the model), generated text that quotes library lyrics is withheld and duplicates are merged. Nothing the
/// model says is trusted; every dropped item is counted.
/// </summary>
public static class SuggestionValidator
{
    /// <summary>Largest share of a line a correction may change.</summary>
    public const double MaxCorrectionShare = 0.6;

    /// <summary>Longest title idea.</summary>
    public const int MaxTitleLength = 80;

    /// <summary>Validates one block's output.</summary>
    /// <param name="context">Job, block, verification and group id.</param>
    /// <param name="output">The parsed output.</param>
    /// <returns>The block result (skipped = false).</returns>
    public static LyricBlockResult Validate(ValidationContext context, ParsedLyricOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);
        var tally = new Tally { InvalidSchema = output.InvalidItems };
        var accepted = new List<AssistantSuggestion>();
        foreach (var item in output.Items)
        {
            var candidate = new SuggestionCandidate(context, item);
            if (candidate.Check() is { } reason)
            {
                tally.Add(reason);
            }
            else if (CountAllows(context.Job, accepted, tally) && !accepted.Any(candidate.Duplicates))
            {
                accepted.Add(candidate.ToSuggestion());
            }
        }

        var misses = MissVerdicts(context.Block, output.Misses, tally);
        return new LyricBlockResult { BlockIndex = context.Block.Index, Suggestions = accepted, Misses = misses, Discarded = tally.ToDto() };
    }

    /// <summary>The verdicts that name a miss of the block (same line and word), at most one per miss, with the required fields.</summary>
    private static List<ModelMissVerdict> MissVerdicts(LyricBlock block, IReadOnlyList<ModelMissVerdict> verdicts, Tally tally)
    {
        var result = new List<ModelMissVerdict>();
        foreach (var verdict in verdicts)
        {
            if (!block.Misses.Any(miss => miss.Line == verdict.Line && miss.Word == verdict.Word))
            {
                tally.Add(DiscardReason.InvalidAnchor);
            }
            else if (!Complete(verdict))
            {
                tally.Add(DiscardReason.InvalidSchema);
            }
            else if (!result.Any(kept => kept.Line == verdict.Line && kept.Word == verdict.Word))
            {
                result.Add(verdict);
            }
        }

        return result;
    }

    private static bool Complete(ModelMissVerdict verdict) => verdict.Verdict switch
    {
        MissVerdict.Typo => !string.IsNullOrWhiteSpace(verdict.Correction) && verdict.Correction != verdict.Word,
        MissVerdict.Licence => verdict.LicenceType is not null,
        _ => true,
    };

    /// <summary>Writing help stays within the requested count (alternatives 1-5, titles 1-5, one continuation per block).</summary>
    private static bool CountAllows(LyricAssistJob job, List<AssistantSuggestion> accepted, Tally tally)
    {
        var limit = job.Kind switch
        {
            AssistKindDto.Alternatives => Math.Clamp(job.Count ?? LyricAssistLimits.DefaultAlternatives, 1, LyricAssistLimits.MaxAlternatives),
            AssistKindDto.Titles => Math.Clamp(job.Count ?? LyricAssistLimits.DefaultTitles, 1, LyricAssistLimits.MaxTitles),
            AssistKindDto.Continue => 1,
            _ => int.MaxValue,
        };
        if (accepted.Count < limit)
        {
            return true;
        }

        tally.Add(DiscardReason.InvalidSchema);
        return false;
    }

    private sealed class Tally
    {
        private int _anchor;
        private int _verification;
        private int _verbatim;

        public int InvalidSchema { get; set; }

        public void Add(DiscardReason reason)
        {
            switch (reason)
            {
                case DiscardReason.InvalidAnchor: _anchor++; break;
                case DiscardReason.FailedVerification: _verification++; break;
                case DiscardReason.Verbatim: _verbatim++; break;
                default: InvalidSchema++; break;
            }
        }

        public DiscardedCountsDto ToDto() => new() { InvalidAnchor = _anchor, InvalidSchema = InvalidSchema, FailedVerification = _verification, Verbatim = _verbatim };
    }
}
