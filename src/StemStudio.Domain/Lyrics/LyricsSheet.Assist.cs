using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Lyrics;

/// <summary>Feature 023 members of a sheet: revision, sensitivity, rhythm target, marks, own-work override, assisted lines.</summary>
public sealed partial class LyricsSheet
{
    private readonly List<IntentionalMark> _marks = [];
    private readonly List<AssistedLine> _assistedLines = [];

    /// <summary>Revision; +1 on every save, accept, undo and restore (existing sheets start at 1).</summary>
    public int Revision { get; private set; }

    /// <summary>Cause of the current revision (undo is allowed only after an accept).</summary>
    public RevisionCause LastCause { get; private set; }

    /// <summary>Sensitivity of the checker.</summary>
    public Sensitivity Sensitivity { get; private set; } = Sensitivity.Balanced;

    /// <summary>Rhythm target, if set.</summary>
    public RhythmTarget? RhythmTarget { get; private set; }

    /// <summary>Own-work override, if confirmed.</summary>
    public OwnWorkOverride? OwnWorkOverride { get; private set; }

    /// <summary>Intentional marks.</summary>
    public IReadOnlyList<IntentionalMark> IntentionalMarks => _marks;

    /// <summary>Lines produced by accepted suggestions and not edited since.</summary>
    public IReadOnlyList<AssistedLine> AssistedLines => _assistedLines;

    /// <summary>The assist language, or null when the assistant does not support the sheet's language.</summary>
    public AssistLanguage? AssistLanguage => AssistEnumExtensions.AssistLanguageOf(Language);

    /// <summary>True when the newest change is an accept (one undo step).</summary>
    public bool CanUndo => LastCause == RevisionCause.Accept;

    /// <summary>Stores the result of an accept as a new revision and records its assisted lines.</summary>
    /// <param name="outcome">A successful outcome of <see cref="SuggestionApplier.Apply"/>.</param>
    /// <param name="nowUtc">Current time.</param>
    public void ApplyAccepted(ApplyOutcome outcome, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        Change(new LyricsContent(outcome.Title, Language, outcome.Text), RevisionCause.Accept, nowUtc);
        foreach (var line in outcome.AssistedLines)
        {
            _assistedLines.RemoveAll(existing => existing.LineText == line.LineText);
            _assistedLines.Add(line);
        }

        _assistedLines.RemoveRange(0, Math.Max(_assistedLines.Count - AssistedLine.MaxPerSheet, 0));
    }

    /// <summary>Restores an earlier title and text as a new revision (cause undo); assisted lines of the given suggestions are removed.</summary>
    /// <param name="title">Title.</param>
    /// <param name="text">Text.</param>
    /// <param name="removedSuggestions">Suggestions whose assisted lines go.</param>
    /// <param name="nowUtc">Current time.</param>
    public void Restore(string title, string text, IReadOnlyCollection<Guid> removedSuggestions, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(removedSuggestions);
        _assistedLines.RemoveAll(line => removedSuggestions.Contains(line.SuggestionId));
        Change(new LyricsContent(title, Language, text), RevisionCause.Undo, nowUtc);
    }

    /// <summary>Sets the sensitivity and/or the rhythm target (text and revision unchanged).</summary>
    /// <param name="sensitivity">New sensitivity, or null to keep.</param>
    /// <param name="setRhythm">True to replace the rhythm target.</param>
    /// <param name="rhythmTarget">The new target (null clears).</param>
    public void SetAssistSettings(Sensitivity? sensitivity, bool setRhythm, RhythmTarget? rhythmTarget)
    {
        Sensitivity = sensitivity ?? Sensitivity;
        RhythmTarget = setRhythm ? rhythmTarget : RhythmTarget;
    }

    /// <summary>Adds a mark; an equal mark (scope and text) is returned instead.</summary>
    /// <param name="mark">The mark.</param>
    /// <returns>The stored mark, or null when the sheet already has 200 marks.</returns>
    public IntentionalMark? AddMark(IntentionalMark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);
        if (_marks.Find(existing => existing.Scope == mark.Scope && existing.Text == mark.Text) is { } same)
        {
            return same;
        }

        if (_marks.Count >= IntentionalMark.MaxPerSheet)
        {
            return null;
        }

        _marks.Add(mark);
        return mark;
    }

    /// <summary>Removes a mark.</summary>
    /// <param name="markId">Mark id.</param>
    /// <returns>True when removed.</returns>
    public bool RemoveMark(Guid markId) => _marks.RemoveAll(mark => mark.Id == markId) > 0;

    /// <summary>Stores the own-work override (replacing an earlier one).</summary>
    /// <param name="ownWork">The override.</param>
    public void ConfirmOwnWork(OwnWorkOverride ownWork) => OwnWorkOverride = ownWork ?? throw new ArgumentNullException(nameof(ownWork));

    /// <summary>Revokes the own-work override.</summary>
    /// <returns>True when there was one.</returns>
    public bool RevokeOwnWork()
    {
        var had = OwnWorkOverride is not null;
        OwnWorkOverride = null;
        return had;
    }

    /// <summary>The assisted lines with their current 1-based line numbers (every line with that text).</summary>
    /// <returns>Line number and suggestion id pairs in line order.</returns>
    public IReadOnlyList<(int Line, Guid SuggestionId)> AssistedLineNumbers()
    {
        var byText = _assistedLines.ToDictionary(line => line.LineText, line => line.SuggestionId, StringComparer.Ordinal);
        return SheetText.Lines(Text)
            .Select((line, index) => (Line: index + 1, Found: byText.TryGetValue(line, out var id), Id: id))
            .Where(entry => entry.Found)
            .Select(entry => (entry.Line, entry.Id))
            .ToList();
    }

    private void Change(LyricsContent valid, RevisionCause cause, DateTime nowUtc)
    {
        (Title, Language, Text, UpdatedAt) = (valid.Title, valid.Language, valid.Text, nowUtc);
        (Revision, LastCause) = (Revision + 1, cause);
        var lines = SheetText.Lines(Text).ToHashSet(StringComparer.Ordinal);
        _assistedLines.RemoveAll(line => !lines.Contains(line.LineText));
    }
}
