using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Api;
using StemStudio.Web.Styles;

namespace StemStudio.Web.Components.Styles;

/// <summary>Creates or updates an own style and returns to the library (feature 009, US3).</summary>
public sealed partial class StyleEditor
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>The own style to edit; null for a new style.</summary>
    [Parameter]
    public StyleDto? Style { get; set; }

    /// <summary>The page heading.</summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    private StyleEditorForm Form { get; set; } = new();

    private FieldProblem? Problem { get; set; }

    private bool Busy { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Form = Style is null ? new StyleEditorForm() : StyleEditorForm.From(Style);

    private bool TryAddTag(List<string> tags, string field, string text)
    {
        Problem = StyleEditorForm.AddTag(tags, field, text);
        return Problem is null;
    }

    private void Cancel() => Navigation.NavigateTo("styles");

    private async Task SaveAsync()
    {
        Problem = Form.Validate();
        if (Problem is null)
        {
            Busy = true;
            await SendAsync();
            Busy = false;
        }
    }

    private async Task SendAsync()
    {
        try
        {
            _ = Style is null ? await Api.CreateStyleAsync(Form.ToInput()) : await Api.UpdateStyleAsync(Style.Id, Form.ToInput());
            Navigation.NavigateTo("styles");
        }
        catch (ApiException ex)
        {
            Problem = FieldProblem.From(ex.Error);
        }
    }
}
