using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace StemStudio.Web.Components.Styles;

/// <summary>A list of short tags with an input that adds one tag per Enter (feature 009 style editor).</summary>
public sealed partial class TagInput
{
    /// <summary>Visible label, e.g. "Moods".</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary><c>data-testid</c> (and element id) of the text input.</summary>
    [Parameter, EditorRequired]
    public string TestId { get; set; } = string.Empty;

    /// <summary><c>data-testid</c> of the tag list.</summary>
    [Parameter, EditorRequired]
    public string ListTestId { get; set; } = string.Empty;

    /// <summary>The current tags.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<string> Tags { get; set; } = [];

    /// <summary>Tries to add the typed tag; returns false when it was refused (the draft is kept).</summary>
    [Parameter, EditorRequired]
    public Func<string, bool> TryAdd { get; set; } = default!;

    /// <summary>Raised after an add attempt so the editor shows the tags or the problem.</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    /// <summary>Remove pressed for a tag.</summary>
    [Parameter]
    public EventCallback<string> OnRemove { get; set; }

    private string Draft { get; set; } = string.Empty;

    private Task OnKeyAsync(KeyboardEventArgs args) => args.Key == "Enter" ? AddAsync() : Task.CompletedTask;

    private Task AddAsync()
    {
        if (TryAdd(Draft))
        {
            Draft = string.Empty;
        }

        return OnChanged.InvokeAsync();
    }
}
