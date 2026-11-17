using System.Globalization;
using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Practice;

/// <summary>One row of the section editor.</summary>
public sealed class SectionRow
{
    /// <summary>Kept id (null for a new section).</summary>
    public Guid? Id { get; init; }

    /// <summary>Kind.</summary>
    public SectionKindDto Kind { get; set; }

    /// <summary>Label (1..40 characters).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>True when the user typed the label; auto labels are renumbered.</summary>
    public bool Named { get; set; }

    /// <summary>Start in seconds.</summary>
    public double Start { get; set; }

    /// <summary>End in seconds.</summary>
    public double End { get; set; }

    /// <summary>Instrument channel of a solo, or null.</summary>
    public string? Instrument { get; set; }
}

/// <summary>
/// The section editor's working copy (014 US3, FR-006): split at the playhead (snapped to the grid of the set), merge with
/// the next (keeps the first id, kind and label), rename, change kind (instrument only for solos), move a boundary with a
/// 1 s minimum on both sides, delete (the previous section takes the time), add after the last; auto labels are
/// renumbered after each change unless the user named them. Saved as a whole with the set's revision.
/// </summary>
public sealed class SectionDraft
{
    /// <summary>Shortest section in seconds.</summary>
    public const double MinLength = 1;

    /// <summary>Most sections.</summary>
    public const int MaxSections = 64;

    /// <summary>Longest label.</summary>
    public const int MaxLabel = 40;

    private static readonly HashSet<SectionKindDto> AlwaysNumbered = [SectionKindDto.Verse, SectionKindDto.PreChorus, SectionKindDto.Chorus, SectionKindDto.Bridge];

    private readonly List<SectionRow> _rows;
    private readonly IReadOnlyList<double> _grid;

    /// <summary>Starts a draft from a section set.</summary>
    /// <param name="set">The stored set.</param>
    /// <param name="duration">Song length in seconds.</param>
    /// <param name="beatGrid">The track's beat grid (for snapping), or null.</param>
    public SectionDraft(SectionSetDto set, double duration, BeatGridDto? beatGrid)
    {
        ArgumentNullException.ThrowIfNull(set);
        Revision = set.Revision;
        Duration = duration;
        _grid = set.Snap switch
        {
            SectionSnapDto.Downbeat => beatGrid?.Downbeats ?? [],
            SectionSnapDto.Beat => beatGrid?.Beats ?? [],
            _ => [],
        };
        _rows = [.. set.Sections.Select(s => new SectionRow
        {
            Id = s.Id, Kind = s.Kind, Label = s.Label, Named = !IsAutoLabel(s.Label, s.Kind, s.Instrument), Start = s.StartSeconds, End = s.EndSeconds,
            Instrument = s.Instrument,
        })];
    }

    /// <summary>The revision the draft was made from.</summary>
    public int Revision { get; }

    /// <summary>Song length.</summary>
    public double Duration { get; }

    /// <summary>The rows in order.</summary>
    public IReadOnlyList<SectionRow> Rows => _rows;

    /// <summary>True when another section can be added after the last one.</summary>
    public bool CanAdd => _rows.Count < MaxSections && Duration - (_rows.Count == 0 ? 0 : _rows[^1].End) >= MinLength;

    /// <summary>The auto label of a kind (without number).</summary>
    /// <param name="kind">Kind.</param>
    /// <param name="instrument">Solo instrument, or null.</param>
    /// <returns>For example "Pre-chorus" or "Guitar solo".</returns>
    public static string BaseLabel(SectionKindDto kind, string? instrument)
    {
        if (kind == SectionKindDto.Solo && instrument is { Length: > 0 })
        {
            return $"{char.ToUpperInvariant(instrument[0])}{instrument[1..].Replace('.', ' ')} solo";
        }

        var text = SectionText.Kind(kind);
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    /// <summary>True for a label the editor may renumber.</summary>
    /// <param name="label">The label.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="instrument">Solo instrument.</param>
    /// <returns>Whether it is an auto label.</returns>
    public static bool IsAutoLabel(string label, SectionKindDto kind, string? instrument)
    {
        ArgumentNullException.ThrowIfNull(label);
        var stem = BaseLabel(kind, instrument);
        return label == stem || (label.StartsWith(stem + " ", StringComparison.Ordinal) && int.TryParse(label[(stem.Length + 1)..], CultureInfo.InvariantCulture, out _));
    }

    /// <summary>Splits the section at the position (snapped); refused within 1 s of either end.</summary>
    /// <param name="position">Playhead in seconds.</param>
    /// <returns>True when split.</returns>
    public bool Split(double position)
    {
        var at = Snap(position);
        var index = _rows.FindIndex(r => at - r.Start >= MinLength && r.End - at >= MinLength);
        if (index < 0)
        {
            return false;
        }

        var row = _rows[index];
        _rows.Insert(index + 1, new SectionRow { Kind = row.Kind, Start = at, End = row.End, Instrument = row.Instrument });
        row.End = at;
        Renumber();
        return true;
    }

    /// <summary>Merges a row with the next one (the first keeps its id, kind and label).</summary>
    /// <param name="index">Row index.</param>
    public void Merge(int index)
    {
        if (index < 0 || index >= _rows.Count - 1)
        {
            return;
        }

        _rows[index].End = _rows[index + 1].End;
        _rows.RemoveAt(index + 1);
        Renumber();
    }

    /// <summary>Deletes a row; the previous row (or the next for the first) takes its time.</summary>
    /// <param name="index">Row index.</param>
    public void Delete(int index)
    {
        if (index < 0 || index >= _rows.Count)
        {
            return;
        }

        var removed = _rows[index];
        _rows.RemoveAt(index);
        if (index > 0 && _rows[index - 1].End == removed.Start)
        {
            _rows[index - 1].End = removed.End;
        }
        else if (index < _rows.Count && _rows[index].Start == removed.End)
        {
            _rows[index].Start = removed.Start;
        }

        Renumber();
    }

    /// <summary>Adds a section from the end of the last one to the end of the song.</summary>
    public void Add()
    {
        if (!CanAdd)
        {
            return;
        }

        var start = _rows.Count == 0 ? 0 : _rows[^1].End;
        _rows.Add(new SectionRow { Kind = SectionKindDto.Other, Start = start, End = Duration });
        Renumber();
    }

    /// <summary>Renames a row; an empty name gives the auto label back.</summary>
    /// <param name="index">Row index.</param>
    /// <param name="label">The typed label.</param>
    public void Rename(int index, string? label)
    {
        var text = (label ?? string.Empty).Trim();
        _rows[index].Named = text.Length > 0;
        _rows[index].Label = text.Length > MaxLabel ? text[..MaxLabel] : text;
        Renumber();
    }

    /// <summary>Changes the kind; a non-solo loses its instrument.</summary>
    /// <param name="index">Row index.</param>
    /// <param name="kind">The kind.</param>
    public void SetKind(int index, SectionKindDto kind)
    {
        _rows[index].Kind = kind;
        _rows[index].Instrument = kind == SectionKindDto.Solo ? _rows[index].Instrument : null;
        Renumber();
    }

    /// <summary>Sets the instrument of a solo.</summary>
    /// <param name="index">Row index.</param>
    /// <param name="instrument">Channel id, or null/empty for none.</param>
    public void SetInstrument(int index, string? instrument)
    {
        _rows[index].Instrument = _rows[index].Kind == SectionKindDto.Solo && !string.IsNullOrEmpty(instrument) ? instrument : null;
        Renumber();
    }

    /// <summary>Moves the start of a row (and the end of an adjacent previous row), keeping 1 s on both sides.</summary>
    /// <param name="index">Row index.</param>
    /// <param name="start">The wanted start in seconds.</param>
    /// <returns>The start that was applied.</returns>
    public double MoveStart(int index, double start)
    {
        var row = _rows[index];
        var previous = index > 0 ? _rows[index - 1] : null;
        var adjacent = previous is not null && previous.End == row.Start;
        var low = previous is null ? 0 : (adjacent ? previous.Start : previous.End) + (adjacent ? MinLength : 0);
        var value = Math.Clamp(Math.Round(start, 1), low, row.End - MinLength);
        if (adjacent)
        {
            previous!.End = value;
        }

        row.Start = value;
        return value;
    }

    /// <summary>The first problem that blocks saving, or null (FR-006 rules of <c>replaceSections</c>).</summary>
    /// <returns>The message.</returns>
    public string? Problem()
    {
        if (_rows.Count is 0 or > MaxSections)
        {
            return $"Keep between 1 and {MaxSections} sections.";
        }

        return _rows.FirstOrDefault(r => r.End - r.Start < MinLength) is { } shortRow
            ? $"\"{shortRow.Label}\" is shorter than one second."
            : null;
    }

    /// <summary>The update request.</summary>
    /// <returns>The request with the draft's revision.</returns>
    public SectionsUpdateRequest ToRequest() => new()
    {
        Revision = Revision,
        Sections = [.. _rows.Select(r => new SectionInputDto
        {
            Id = r.Id, Kind = r.Kind, Label = r.Label, StartSeconds = r.Start, EndSeconds = r.End, Instrument = r.Kind == SectionKindDto.Solo ? r.Instrument : null,
        })],
    };

    private double Snap(double position)
    {
        var nearest = _grid.Count == 0 ? position : _grid.MinBy(t => Math.Abs(t - position));
        return Math.Round(nearest, 1);
    }

    private void Renumber()
    {
        foreach (var group in _rows.Where(r => !r.Named).GroupBy(r => BaseLabel(r.Kind, r.Instrument)))
        {
            var rows = group.ToList();
            var numbered = AlwaysNumbered.Contains(rows[0].Kind) || rows.Count > 1;
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].Label = numbered ? string.Create(CultureInfo.InvariantCulture, $"{group.Key} {i + 1}") : group.Key;
            }
        }
    }
}
