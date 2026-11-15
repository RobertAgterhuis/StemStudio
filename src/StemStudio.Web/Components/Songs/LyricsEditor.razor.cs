using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StemStudio.Web.Songs;

namespace StemStudio.Web.Components.Songs;

/// <summary>The lyrics part of the song page (feature 010); the page runs the API calls.</summary>
public sealed partial class LyricsEditor
{
    /// <summary>The page's form.</summary>
    [Parameter, EditorRequired]
    public SongForm Form { get; set; } = default!;

    /// <summary>True while a helper call runs.</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Title of the sheet saved last, for the confirmation.</summary>
    [Parameter]
    public string? SavedTitle { get; set; }

    /// <summary>A saved sheet (or "New lyrics" = null) was chosen.</summary>
    [Parameter]
    public EventCallback<Guid?> OnSelect { get; set; }

    /// <summary>The text changed.</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    /// <summary>"Add sections" pressed.</summary>
    [Parameter]
    public EventCallback OnPropose { get; set; }

    /// <summary>"Save lyrics" pressed.</summary>
    [Parameter]
    public EventCallback OnSave { get; set; }

    /// <summary>A file was chosen.</summary>
    [Parameter]
    public EventCallback<InputFileChangeEventArgs> OnUpload { get; set; }

    private Task OnTextAsync(ChangeEventArgs args)
    {
        Form.Text = (string)args.Value!;
        return OnChanged.InvokeAsync();
    }

    private static Guid? ParseId(ChangeEventArgs args) => Guid.TryParse(args.Value as string, out var id) ? id : null;
}
