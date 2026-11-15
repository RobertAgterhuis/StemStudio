using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Services;

/// <summary>Readable lookup states and their contract values (feature 005, FR-009, ui-test-ids 1.4.0).</summary>
public static class MetadataStatusText
{
    /// <summary>The default reason of a failed lookup (worker <c>METADATA_FAILED_TEXT</c>).</summary>
    public const string FailedText = "Band information could not be retrieved; try again later.";

    /// <summary>The <c>data-status</c> value, e.g. <c>notFound</c>.</summary>
    /// <param name="status">The lookup state.</param>
    /// <returns>The camelCase contract value.</returns>
    public static string Value(MetadataStatusDto status) => status switch
    {
        MetadataStatusDto.NotFound => "notFound",
        _ => status.ToString().ToLowerInvariant(),
    };

    /// <summary>A short readable status for the track page.</summary>
    /// <param name="status">The lookup state.</param>
    /// <returns>The text.</returns>
    public static string Label(MetadataStatusDto status) => status switch
    {
        MetadataStatusDto.Pending => "Waiting to look up band information",
        MetadataStatusDto.Searching => "Searching for band information",
        MetadataStatusDto.Found => "Band information found",
        MetadataStatusDto.NotFound => "No band information found",
        MetadataStatusDto.Failed => "Band information could not be retrieved",
        MetadataStatusDto.Disabled => "Online lookups are switched off",
        _ => "Band information not looked up yet",
    };

    /// <summary>True while a lookup is scheduled or running, so the page keeps polling.</summary>
    /// <param name="status">The lookup state.</param>
    /// <returns>Whether the state can still change by itself.</returns>
    public static bool IsActive(MetadataStatusDto status) => status is MetadataStatusDto.Pending or MetadataStatusDto.Searching;

    /// <summary>The mixer header album line: "&lt;album&gt; (&lt;year&gt;)" or "&lt;album&gt;"; null unless found with an album.</summary>
    /// <param name="metadata">The track metadata.</param>
    /// <returns>The text, or null.</returns>
    public static string? AlbumLine(TrackMetadataDto metadata) =>
        metadata is { Status: MetadataStatusDto.Found, Album: { } album } ? BandFacts.Album(album, metadata.Year) : null;
}
