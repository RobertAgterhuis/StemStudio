using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Pages;

/// <summary>Track list (<c>/tracks</c>): every imported track with status and links (FR-018).</summary>
public sealed partial class Tracks
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    private IReadOnlyList<TrackDto>? Items { get; set; }

    private string? Error { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Items = await Api.ListTracksAsync();
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }
}
