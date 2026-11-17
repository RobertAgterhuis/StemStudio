namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>A suggestion to apply (copied from the stored suggestion, never from a request).</summary>
/// <param name="Id">Suggestion id.</param>
/// <param name="Anchor">Anchor in the current text.</param>
/// <param name="Operation">Operation.</param>
/// <param name="Proposal">Proposed text (null for <see cref="SuggestionOperation.None"/>).</param>
/// <param name="GroupId">Alternatives group, if any.</param>
public sealed record ApplicableSuggestion(Guid Id, Anchor Anchor, SuggestionOperation Operation, string? Proposal, Guid? GroupId = null);

/// <summary>A line produced by an accepted suggestion (FR-024).</summary>
/// <param name="LineText">The line text (as normalised).</param>
/// <param name="SuggestionId">The suggestion that produced it.</param>
public sealed record AssistedLine(string LineText, Guid SuggestionId)
{
    /// <summary>Longest recorded line.</summary>
    public const int MaxLineTextLength = 400;

    /// <summary>Most assisted lines per sheet.</summary>
    public const int MaxPerSheet = 400;
}

/// <summary>Outcome of <see cref="SuggestionApplier.Apply"/>.</summary>
public sealed record ApplyOutcome
{
    /// <summary>New title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>New normalised text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Applied suggestion ids.</summary>
    public IReadOnlyList<Guid> Applied { get; init; } = [];

    /// <summary>Skipped suggestion ids whose anchor no longer matches.</summary>
    public IReadOnlyList<Guid> Stale { get; init; } = [];

    /// <summary>Suggestions that overlap another one of the request (nothing applied).</summary>
    public IReadOnlyList<Guid> Overlapping { get; init; } = [];

    /// <summary>True when the result exceeds 4,096 characters or the title 80 (nothing applied).</summary>
    public bool TooLong { get; init; }

    /// <summary>Lines produced by the applied suggestions.</summary>
    public IReadOnlyList<AssistedLine> AssistedLines { get; init; } = [];

    /// <summary>True when the text or title may be stored.</summary>
    public bool Succeeded => Overlapping.Count == 0 && !TooLong;
}

/// <summary>
/// Applies suggestions to one revision (data-model 4 "Accept"): replace, insert-after and set-title in descending anchor order so
/// offsets stay valid; overlapping suggestions are refused; the result passes the 010 normaliser and the 4,096 limit.
/// </summary>
public static class SuggestionApplier
{
    /// <summary>Applies the suggestions.</summary>
    /// <param name="title">Current title.</param>
    /// <param name="text">Current normalised text.</param>
    /// <param name="suggestions">The suggestions to apply.</param>
    /// <returns>The outcome.</returns>
    public static ApplyOutcome Apply(string title, string text, IReadOnlyList<ApplicableSuggestion> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        var lines = SheetText.Lines(text).Select(line => new TaggedLine(line, null)).ToList();
        var (valid, stale) = Partition(lines, suggestions);
        var overlapping = Overlaps(valid);
        if (overlapping.Count > 0)
        {
            return new ApplyOutcome { Title = title, Text = text, Overlapping = overlapping };
        }

        var newTitle = title;
        foreach (var suggestion in valid.OrderByDescending(s => s.Anchor.Line).ThenBy(s => s.Operation != SuggestionOperation.InsertAfter).ThenByDescending(s => s.Anchor.Start))
        {
            newTitle = ApplyOne(lines, suggestion) ?? newTitle;
        }

        return Outcome(newTitle.Trim(), lines, valid, stale);
    }

    private static ApplyOutcome Outcome(string title, List<TaggedLine> lines, List<ApplicableSuggestion> valid, List<Guid> stale)
    {
        var normalized = LyricsNormaliser.Normalize(string.Join('\n', lines.Select(line => line.Text)));
        if (normalized.Length is 0 or > LyricsRules.MaxTextLength || title.Length is 0 or > LyricsRules.MaxTitleLength)
        {
            return new ApplyOutcome { TooLong = true, Stale = stale };
        }

        return new ApplyOutcome { Title = title, Text = normalized, Applied = valid.Select(s => s.Id).ToList(), Stale = stale, AssistedLines = Assisted(lines) };
    }

    private static List<AssistedLine> Assisted(List<TaggedLine> lines) => lines
        .Where(line => line.SuggestionId is not null && line.Text.TrimEnd().Length is > 0 and <= AssistedLine.MaxLineTextLength)
        .Select(line => new AssistedLine(line.Text.TrimEnd(), line.SuggestionId!.Value))
        .DistinctBy(line => line.LineText, StringComparer.Ordinal)
        .ToList();

    private static (List<ApplicableSuggestion> Valid, List<Guid> Stale) Partition(List<TaggedLine> lines, IReadOnlyList<ApplicableSuggestion> suggestions)
    {
        var valid = suggestions.Where(s => Matches(lines, s)).ToList();
        return (valid, suggestions.Except(valid).Select(s => s.Id).ToList());
    }

    private static bool Matches(List<TaggedLine> lines, ApplicableSuggestion suggestion) => suggestion.Operation switch
    {
        SuggestionOperation.SetTitle or SuggestionOperation.None => true,
        _ => suggestion.Anchor.Line >= 1 && suggestion.Anchor.Line <= lines.Count && suggestion.Anchor.MatchesIn(lines[suggestion.Anchor.Line - 1].Text),
    };

    private static List<Guid> Overlaps(List<ApplicableSuggestion> suggestions)
    {
        var overlapping = new HashSet<Guid>();
        for (var i = 0; i < suggestions.Count; i++)
        {
            for (var j = i + 1; j < suggestions.Count; j++)
            {
                if (Overlap(suggestions[i], suggestions[j]))
                {
                    overlapping.UnionWith([suggestions[i].Id, suggestions[j].Id]);
                }
            }
        }

        return [.. suggestions.Select(s => s.Id).Where(overlapping.Contains)];
    }

    private static bool Overlap(ApplicableSuggestion a, ApplicableSuggestion b)
    {
        if (a.GroupId is not null && a.GroupId == b.GroupId)
        {
            return true;
        }

        if (a.Operation != b.Operation || a.Operation == SuggestionOperation.None)
        {
            return false;
        }

        return a.Operation switch
        {
            SuggestionOperation.SetTitle => true,
            SuggestionOperation.InsertAfter => a.Anchor.Line == b.Anchor.Line,
            _ => a.Anchor.Line == b.Anchor.Line && RangesOverlap(a.Anchor, b.Anchor),
        };
    }

    private static bool RangesOverlap(Anchor a, Anchor b) =>
        (a.Start < b.End && b.Start < a.End) || a.Start == b.Start;

    private static string? ApplyOne(List<TaggedLine> lines, ApplicableSuggestion suggestion)
    {
        var proposal = suggestion.Proposal ?? string.Empty;
        switch (suggestion.Operation)
        {
            case SuggestionOperation.SetTitle:
                return proposal;
            case SuggestionOperation.InsertAfter:
                lines.InsertRange(suggestion.Anchor.Line, Tagged(proposal, suggestion.Id));
                return null;
            case SuggestionOperation.Replace:
                Replace(lines, suggestion.Anchor, proposal, suggestion.Id);
                return null;
            default:
                return null;
        }
    }

    private static void Replace(List<TaggedLine> lines, Anchor anchor, string proposal, Guid id)
    {
        var line = lines[anchor.Line - 1].Text;
        var replaced = string.Concat(line.AsSpan(0, anchor.Start), proposal, line.AsSpan(anchor.End));
        lines.RemoveAt(anchor.Line - 1);
        lines.InsertRange(anchor.Line - 1, Tagged(replaced, id));
    }

    private static IEnumerable<TaggedLine> Tagged(string text, Guid id) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => new TaggedLine(line, id));

    private sealed record TaggedLine(string Text, Guid? SuggestionId);
}
