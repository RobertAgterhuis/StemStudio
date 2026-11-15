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

    /// <summary>Rename pressed.</summary>
    [Parameter]
    public EventCallback<AssetDto> OnRename { get; set; }

    /// <summary>Delete pressed (the page confirms).</summary>
    [Parameter]
    public EventCallback<AssetDto> OnDelete { get; set; }

    /// <summary>Retry pressed (failed items only).</summary>
    [Parameter]
    public EventCallback<AssetDto> OnRetry { get; set; }

    private string Facts => AssetText.Format(Asset.Audio) is { } format
        ? $" · {format} · {TimeText.Duration(Asset.Audio!.DurationSeconds)} · {ByteSize.Format(Asset.SizeBytes)}"
        : string.Empty;

    private string FileUrl(bool inline) =>
        new Uri(Options.Value.PublicApiBaseUrl, ApiRoutes.AssetFile(Asset.Id, ApiRoutes.AssetFileAudio, inline)).ToString();
}
