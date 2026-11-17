using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Library;

namespace StemStudio.Web.Components.Library;

/// <summary>Parameters, focus and keys of the live search box.</summary>
public sealed partial class LibrarySearchBox
{
    private ElementReference _input;

    /// <summary>The live search.</summary>
    [Parameter, EditorRequired]
    public LiveSearchController Controller { get; set; } = default!;

    /// <summary>The announced count text.</summary>
    [Parameter]
    public string Status { get; set; } = string.Empty;

    /// <summary>The open folder (for "Only in this folder"), or null at the root.</summary>
    [Parameter]
    public string? FolderId { get; set; }

    /// <summary>Arrow Down in the box: the page moves focus to the first result.</summary>
    [Parameter]
    public EventCallback OnArrowDown { get; set; }

    /// <summary>Moves focus into the box (the <c>/</c> shortcut, the missing-folder link, after clearing).</summary>
    /// <returns>A task that completes when focus moved.</returns>
    public ValueTask FocusAsync() => _input.FocusAsync();

    private void OnInput(ChangeEventArgs args) => Controller.Input(args.Value?.ToString());

    private Task OnKeyAsync(KeyboardEventArgs args) => args.Key switch
    {
        "Enter" => Controller.SubmitAsync(),
        "Escape" => Controller.ClearAsync(),
        "ArrowDown" when Controller.IsActive => OnArrowDown.InvokeAsync(),
        _ => Task.CompletedTask,
    };

    private Task OnScopeAsync(ChangeEventArgs args) => Controller.ScopeAsync(args.Value is true ? FolderId : null);
}
