using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Api;
using StemStudio.Web.Styles;

namespace StemStudio.Web.Components.Styles;

/// <summary>Loads the style library and applies the filters (feature 009).</summary>
public sealed partial class StylesPage
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    private IReadOnlyList<StyleDto>? Items { get; set; }

    private string? Error { get; set; }

    private StyleFilter Filter { get; } = new();

    private IReadOnlyList<StyleDto> Visible => Filter.Apply(Items!);

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        try
        {
            (Items, Error) = (await Api.ListStylesAsync(), null);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }
}
