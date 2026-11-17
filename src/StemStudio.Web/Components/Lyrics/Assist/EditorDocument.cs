using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// The sheet open in the lyrics editor page (<c>/lyrics/{id}</c>): the saved sheet and the edited title, language and text. The
/// assistant works on the saved revision only, so <see cref="IsDirty"/> tells it to save first (023 US1 scenario 7).
/// </summary>
/// <param name="sheet">The loaded sheet.</param>
public sealed class EditorDocument(LyricsSheetDto sheet)
{
    /// <summary>The sheet as saved.</summary>
    public LyricsSheetDto Sheet { get; private set; } = sheet;

    /// <summary>The edited title.</summary>
    public string Title { get; set; } = sheet.Title;

    /// <summary>The edited language code.</summary>
    public string Language { get; set; } = sheet.Language;

    /// <summary>The edited text.</summary>
    public string Text { get; set; } = sheet.Text;

    /// <summary>The sheet id.</summary>
    public Guid Id => Sheet.Id;

    /// <summary>The saved revision.</summary>
    public int Revision => Sheet.Revision;

    /// <summary>The saved assistant info (sensitivity, rhythm target, undo, own-work override).</summary>
    public SheetAssistInfoDto Assist => Sheet.Assist;

    /// <summary>True when the title, language or text differ from the saved sheet.</summary>
    public bool IsDirty => Title != Sheet.Title || Language != Sheet.Language || Text != Sheet.Text;

    /// <summary>The assistant language of the saved sheet, or null for other languages and <c>unknown</c> (FR-001).</summary>
    public AssistLanguageDto? AssistLanguage => Sheet.Language switch
    {
        "nl" => AssistLanguageDto.Nl,
        "en" => AssistLanguageDto.En,
        _ => null,
    };

    /// <summary>Replaces the saved sheet and the edited values (load, save, reload after a conflict).</summary>
    /// <param name="saved">The sheet.</param>
    public void Load(LyricsSheetDto saved)
    {
        Sheet = saved;
        (Title, Language, Text) = (saved.Title, saved.Language, saved.Text);
    }

    /// <summary>Takes the result of an accept, undo or restore: the new revision, title, text and assistant info.</summary>
    /// <param name="result">The result.</param>
    public void Apply(AcceptResultDto result) =>
        Load(Sheet with { Revision = result.Revision, Title = result.Title, Text = result.Text, Assist = result.Assist });

    /// <summary>Takes new assistant info (settings, own-work override).</summary>
    /// <param name="assist">The info.</param>
    public void SetAssist(SheetAssistInfoDto assist) => Sheet = Sheet with { Assist = assist };

    /// <summary>The 010 save body with the expected revision (409 <c>revision-conflict</c> when another tab saved first).</summary>
    /// <returns>The input.</returns>
    public LyricsInput ToInput() => new() { Title = Title.Trim(), Language = Language, Text = Text, ExpectedRevision = Revision };
}
