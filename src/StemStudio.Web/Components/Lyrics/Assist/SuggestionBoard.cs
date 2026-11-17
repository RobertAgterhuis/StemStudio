using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// The suggestions of the open sheet as the pane shows them: merged from the stream, the assist snapshot and the suggestion
/// list; the category filter (FR-029, Advanced); the focused entry for F8 / Shift+F8 (FR-028).
/// </summary>
public sealed class SuggestionBoard
{
    private readonly Dictionary<Guid, LyricSuggestionDto> _items = [];
    private readonly HashSet<SuggestionCategoryDto> _filter = [];

    /// <summary>The focused suggestion, or null.</summary>
    public Guid? Active { get; private set; }

    /// <summary>The categories of the filter; empty shows all.</summary>
    public IReadOnlySet<SuggestionCategoryDto> Filter => _filter;

    /// <summary>Every open suggestion in text order (title ideas first).</summary>
    public IReadOnlyList<LyricSuggestionDto> Open => [.. Ordered(_items.Values.Where(s => s.Status == SuggestionStatusDto.Open))];

    /// <summary>The open suggestions that pass the filter, in text order.</summary>
    public IReadOnlyList<LyricSuggestionDto> Visible => [.. Open.Where(s => _filter.Count == 0 || _filter.Contains(s.Category))];

    /// <summary>The suggestions "Accept all" applies: visible replacements, one per alternatives group, at most 100.</summary>
    public IReadOnlyList<Guid> AcceptAllIds => [.. Visible
        .Where(s => s.Operation == SuggestionOperationDto.Replace)
        .DistinctBy(s => s.GroupId ?? s.SuggestionId)
        .Take(LyricAssistLimits.MaxDecisionIds)
        .Select(s => s.SuggestionId)];

    /// <summary>The open suggestions of one line, for the highlight overlay.</summary>
    /// <param name="line">The 1-based line.</param>
    /// <returns>The suggestions.</returns>
    public IEnumerable<LyricSuggestionDto> OnLine(int line) => Open.Where(s => s.Anchor.Line == line);

    /// <summary>A suggestion by id, or null.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The suggestion.</returns>
    public LyricSuggestionDto? Find(Guid id) => _items.GetValueOrDefault(id);

    /// <summary>Adds or replaces suggestions.</summary>
    /// <param name="suggestions">The suggestions.</param>
    public void Merge(IEnumerable<LyricSuggestionDto> suggestions)
    {
        foreach (var s in suggestions)
        {
            _items[s.SuggestionId] = s;
        }

        KeepActiveOpen();
    }

    /// <summary>Forgets every suggestion (another sheet, a reload).</summary>
    public void Clear()
    {
        _items.Clear();
        Active = null;
    }

    /// <summary>Switches a category of the filter on or off; null clears the filter ("All").</summary>
    /// <param name="category">The category, or null.</param>
    public void Toggle(SuggestionCategoryDto? category)
    {
        if (category is not { } c)
        {
            _filter.Clear();
        }
        else if (!_filter.Remove(c))
        {
            _filter.Add(c);
        }

        KeepActiveOpen();
    }

    /// <summary>Focuses a suggestion.</summary>
    /// <param name="id">The id, or null.</param>
    public void Focus(Guid? id) => Active = id;

    /// <summary>Moves the focus to the next (or previous) visible suggestion, wrapping around.</summary>
    /// <param name="step">+1 or -1.</param>
    /// <returns>The newly focused suggestion, or null when none is visible.</returns>
    public LyricSuggestionDto? Move(int step)
    {
        var visible = Visible;
        if (visible.Count == 0)
        {
            Active = null;
            return null;
        }

        var index = Index(visible);
        var next = index < 0 ? (step > 0 ? 0 : visible.Count - 1) : (index + step + visible.Count) % visible.Count;
        Active = visible[next].SuggestionId;
        return visible[next];
    }

    private int Index(IReadOnlyList<LyricSuggestionDto> visible) =>
        Active is { } active ? visible.ToList().FindIndex(s => s.SuggestionId == active) : -1;

    private void KeepActiveOpen()
    {
        if (Active is { } active && Visible.All(s => s.SuggestionId != active))
        {
            Active = null;
        }
    }

    private static IEnumerable<LyricSuggestionDto> Ordered(IEnumerable<LyricSuggestionDto> items) =>
        items.OrderBy(s => s.Anchor.Line).ThenBy(s => s.Anchor.Start).ThenBy(s => s.SuggestionId);
}
