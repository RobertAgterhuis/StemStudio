using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Library;

/// <summary>Display texts, file URLs and actions of one library row.</summary>
public sealed partial class AssetRow
{
    [Inject]
    private IOptions<StemStudioWebOptions> Options { get; set; } = default!;

    /// <summary>The item.</summary>
    [Parameter, EditorRequired]
    public AssetDto Asset { get; set; } = default!;

    /// <summary>True for the item opened from an export confirmation.</summary>
    [Parameter]
    public bool Highlighted { get; set; }

    /// <summary>True to show the item's folder path "Artist > Album > Song" (feature 022, <c>asset-path-&lt;assetId&gt;</c>).</summary>
    [Parameter]
    public bool ShowPath { get; set; }

    /// <summary>The search hit of this item (feature 022): marks the matched parts, lists the matched instruments.</summary>
    [Parameter]
    public StemStudio.Contracts.Library.ItemHitDto? Hit { get; set; }

    /// <summary>"Library info" pressed (feature 022, every level); the button exists only when this is bound.</summary>
    [Parameter]
    public EventCallback<AssetDto> OnLibraryInfo { get; set; }

    /// <summary>Rename pressed.</summary>
    [Parameter]
    public EventCallback<AssetDto> OnRename { get; set; }

    /// <summary>Delete pressed (the page confirms).</summary>
    [Parameter]
    public EventCallback<AssetDto> OnDelete { get; set; }

    /// <summary>Sheet music pressed (ready audio items only, feature 008).</summary>
    [Parameter]
    public EventCallback<AssetDto> OnScore { get; set; }

    /// <summary>Retry pressed (failed items only).</summary>
    [Parameter]
    public EventCallback<AssetDto> OnRetry { get; set; }

    private string Facts => AssetText.Format(Asset.Audio) is { } format
        ? $" · {format} · {TimeText.Duration(Asset.Audio!.DurationSeconds)} · {ByteSize.Format(Asset.SizeBytes)}"
        : string.Empty;

    private IReadOnlyList<StemStudio.Contracts.Library.HighlightDto> Highlights => Hit?.Highlights ?? [];

    private bool IsReadyAudio => Asset is { Kind: AssetKindDto.Audio, Status: AssetStatusDto.Ready };

    private string FileUrl(bool inline) => FileUrl(ApiRoutes.AssetFileAudio, inline);

    /// <summary>The browser URL of one of the item's files.</summary>
    /// <param name="role">The file role, e.g. <see cref="ApiRoutes.AssetFilePdf"/>.</param>
    /// <param name="inline">True to show it in the page instead of downloading it.</param>
    /// <returns>The absolute URL.</returns>
    private string FileUrl(string role, bool inline) =>
        new Uri(Options.Value.PublicApiBaseUrl, ApiRoutes.AssetFile(Asset.Id, role, inline)).ToString();
}
