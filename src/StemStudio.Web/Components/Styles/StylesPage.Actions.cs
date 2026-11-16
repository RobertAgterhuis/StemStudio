using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Styles;

/// <summary>Duplicate and delete in the style library (feature 009, US3-1, US3-4).</summary>
public sealed partial class StylesPage
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>Why the last duplicate or delete failed.</summary>
    private string? ActionError { get; set; }

    /// <summary>The own style waiting for the delete confirmation.</summary>
    private StyleDto? Deleting { get; set; }

    private async Task DuplicateAsync(StyleDto style)
    {
        try
        {
            var copy = await Api.DuplicateStyleAsync(style.Id);
            Navigation.NavigateTo($"styles/{copy.Id}/edit");
        }
        catch (ApiException ex)
        {
            ActionError = ex.Error.UserMessage;
        }
    }

    private async Task DeleteAsync()
    {
        var style = Deleting!;
        Deleting = null;
        try
        {
            await Api.DeleteStyleAsync(style.Id);
            Items = [.. Items!.Where(s => s.Id != style.Id)];
            ActionError = null;
        }
        catch (ApiException ex)
        {
            ActionError = ex.Error.UserMessage;
        }
    }
}
