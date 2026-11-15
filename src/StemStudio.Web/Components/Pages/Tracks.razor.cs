using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Pages;

/// <summary>Track library (<c>/tracks</c>): every imported track with ribbon, status and links (FR-018); search and sort at Advanced+.</summary>
public sealed partial class Tracks
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The cascaded level (Advanced without a shell).</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    private IReadOnlyList<TrackDto>? Items { get; set; }

    private string? Error { get; set; }

    private string Search { get; set; } = string.Empty;

    private TrackSort Sort { get; set; }

    private IReadOnlyList<TrackDto> Visible => TrackListQuery.Apply(Items ?? [], Search, Sort);

    private bool IsExpert => (Experience ?? ExperienceContext.Default).Is(ExperienceLevel.Expert);

    private bool IsBeginner => (Experience ?? ExperienceContext.Default).Is(ExperienceLevel.Beginner);

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

    private void OnSearch(ChangeEventArgs args) => Search = args.Value?.ToString() ?? string.Empty;

    private void OnSort(ChangeEventArgs args) => Sort = TrackListQuery.ParseSort(args.Value?.ToString());
}
