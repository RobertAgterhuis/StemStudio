using StemStudio.Domain.Handouts;

namespace StemStudio.Domain.Tests.Handouts;

/// <summary>Feature 020 T020: practice notes and their text rules (data-model 5.2, FR-015).</summary>
public sealed class PracticeNoteTests
{
    private static readonly DateTime T0 = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("  Keep the eighths even.  ", "Keep the eighths even.")]
    [InlineData("Line one\r\nLine two\rLine three", "Line one\nLine two\nLine three")]
    [InlineData("Tab\there\u0000 and \u001b[31mbell\u0007", "Tabhere and [31mbell")]
    [InlineData("\n\n  \t", "")]
    [InlineData("", "")]
    [InlineData("<b>not html</b> & more", "<b>not html</b> & more")]
    public void Clean_KeepsLineBreaks_AndRemovesOtherControlCharacters(string typed, string stored) =>
        PracticeNoteRules.Clean(typed).ShouldBe(stored);

    [Fact]
    public void Clean_RefusesNull() => Should.Throw<ArgumentNullException>(() => PracticeNoteRules.Clean(null!));

    [Fact]
    public void Create_AndUpdate_StoreTheCleanText()
    {
        var trackId = Guid.NewGuid();
        var note = PracticeNote.Create(trackId, HandoutFamily.Melodic, "Breathe at bar 9.", T0);

        note.Update("Breathe at bar 9.\nTongue the eighths.", T0.AddDays(1));

        (note.TrackId, note.Family, note.Text, note.UpdatedAt).ShouldBe((trackId, HandoutFamily.Melodic, "Breathe at bar 9.\nTongue the eighths.", T0.AddDays(1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" untrimmed ")]
    [InlineData("bell\u0007")]
    public void Create_RefusesTextThatIsNotCleanOrEmpty(string text) =>
        Should.Throw<ArgumentException>(() => PracticeNote.Create(Guid.NewGuid(), HandoutFamily.Bass, text, T0));

    [Fact]
    public void Update_RefusesTextOverTheLimit()
    {
        var note = PracticeNote.Create(Guid.NewGuid(), HandoutFamily.Bass, new string('a', PracticeNoteRules.MaxLength), T0);

        Should.Throw<ArgumentException>(() => note.Update(new string('a', PracticeNoteRules.MaxLength + 1), T0));
        note.Text.Length.ShouldBe(4000);
    }
}
