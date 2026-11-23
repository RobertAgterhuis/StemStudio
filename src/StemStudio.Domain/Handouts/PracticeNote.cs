using System.Text;

namespace StemStudio.Domain.Handouts;

/// <summary>A practice note of one family on a track (data-model 5.2, table <c>PracticeNotes</c>, FR-015).</summary>
public sealed class PracticeNote
{
    private PracticeNote()
    {
        Text = string.Empty;
    }

    /// <summary>The track (cascade delete).</summary>
    public Guid TrackId { get; private set; }

    /// <summary>Family.</summary>
    public HandoutFamily Family { get; private set; }

    /// <summary>Cleaned text (1..4,000 characters).</summary>
    public string Text { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>A new note.</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="family">Family.</param>
    /// <param name="cleanText">Text cleaned by <see cref="PracticeNoteRules.Clean"/> (not empty).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The note.</returns>
    public static PracticeNote Create(Guid trackId, HandoutFamily family, string cleanText, DateTime nowUtc)
    {
        var note = new PracticeNote { TrackId = trackId, Family = family };
        note.Update(cleanText, nowUtc);
        return note;
    }

    /// <summary>Replaces the text.</summary>
    /// <param name="cleanText">Text cleaned by <see cref="PracticeNoteRules.Clean"/> (not empty).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void Update(string cleanText, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(cleanText) || cleanText.Length > PracticeNoteRules.MaxLength || PracticeNoteRules.Clean(cleanText) != cleanText)
        {
            throw new ArgumentException("The note text must be cleaned and not empty.", nameof(cleanText));
        }

        (Text, UpdatedAt) = (cleanText, nowUtc);
    }
}

/// <summary>Text rules of practice notes (FR-015).</summary>
public static class PracticeNoteRules
{
    /// <summary>Longest note.</summary>
    public const int MaxLength = 4000;

    /// <summary>The stored form of a note: line breaks kept (<c>\r\n</c> and <c>\r</c> become <c>\n</c>), every other control character removed, trimmed.</summary>
    /// <param name="text">The text as typed.</param>
    /// <returns>The cleaned text; empty when nothing is left (the note is then deleted).</returns>
    public static string Clean(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var kept = new StringBuilder(normalised.Length);
        foreach (var character in normalised.Where(character => character == '\n' || !char.IsControl(character)))
        {
            kept.Append(character);
        }

        return kept.ToString().Trim();
    }
}
