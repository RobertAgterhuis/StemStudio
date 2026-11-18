using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Components.Recording;

/// <summary>Code-behind of one take row: parameters and the inline rename.</summary>
public sealed partial class TakeRow
{
    private string _draft = string.Empty;

    /// <summary>The take.</summary>
    [Parameter, EditorRequired]
    public TakeItem Item { get; set; } = default!;

    /// <summary>True when it is in the take strip.</summary>
    [Parameter]
    public bool Selected { get; set; }

    /// <summary>True while this row is being renamed.</summary>
    [Parameter]
    public bool Editing { get; set; }

    /// <summary>Why the last rename was refused, or null.</summary>
    [Parameter]
    public string? RenameError { get; set; }

    /// <summary>The radio was chosen.</summary>
    [Parameter]
    public EventCallback OnSelect { get; set; }

    /// <summary>The star was pressed.</summary>
    [Parameter]
    public EventCallback OnStar { get; set; }

    /// <summary>"Play from the start" was pressed.</summary>
    [Parameter]
    public EventCallback OnPlayFrom { get; set; }

    /// <summary>"Rename" was pressed (the panel opens the editor on this row).</summary>
    [Parameter]
    public EventCallback OnRenameStart { get; set; }

    /// <summary>A new name was confirmed.</summary>
    [Parameter]
    public EventCallback<string> OnRename { get; set; }

    /// <summary>The rename was cancelled.</summary>
    [Parameter]
    public EventCallback OnRenameCancel { get; set; }

    /// <summary>"Export" was pressed (US6).</summary>
    [Parameter]
    public EventCallback OnExport { get; set; }

    /// <summary>"Delete" was pressed.</summary>
    [Parameter]
    public EventCallback OnDelete { get; set; }

    /// <summary>"Try again" of a failed take was pressed.</summary>
    [Parameter]
    public EventCallback OnRetry { get; set; }

    private Task StartRenameAsync()
    {
        _draft = Item.Name;
        return OnRenameStart.InvokeAsync();
    }

    private void Draft(ChangeEventArgs args) => _draft = args.Value as string ?? string.Empty;

    private Task ConfirmRenameAsync() => OnRename.InvokeAsync(_draft);

    private Task RenameKeyAsync(KeyboardEventArgs args) => args.Key switch
    {
        "Enter" => ConfirmRenameAsync(),
        "Escape" => OnRenameCancel.InvokeAsync(),
        _ => Task.CompletedTask,
    };
}
