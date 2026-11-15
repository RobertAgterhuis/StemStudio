namespace StemStudio.Contracts.Artists;

/// <summary>A Wikipedia excerpt with its CC BY-SA 4.0 attribution data; shown unmodified with title link, authors link and licence link.</summary>
public sealed record WikipediaSummaryDto
{
    /// <summary>Licence of every excerpt.</summary>
    public const string CcBySa40 = "CC BY-SA 4.0";

    /// <summary>Licence URL of every excerpt.</summary>
    public const string CcBySa40Url = "https://creativecommons.org/licenses/by-sa/4.0/";

    /// <summary>Wiki language code, e.g. <c>en</c>.</summary>
    public required string Language { get; init; }

    /// <summary>Article title.</summary>
    public required string Title { get; init; }

    /// <summary>The excerpt (&lt;= 2000 characters), unmodified.</summary>
    public required string Extract { get; init; }

    /// <summary>Article URL.</summary>
    public required string PageUrl { get; init; }

    /// <summary>History URL, linked as "Wikipedia contributors".</summary>
    public required string HistoryUrl { get; init; }

    /// <summary>Licence name (always <see cref="CcBySa40"/>).</summary>
    public string Licence { get; init; } = CcBySa40;

    /// <summary>Licence URL (always <see cref="CcBySa40Url"/>).</summary>
    public string LicenceUrl { get; init; } = CcBySa40Url;
}
