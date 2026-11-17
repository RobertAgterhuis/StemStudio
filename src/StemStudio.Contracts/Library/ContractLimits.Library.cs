namespace StemStudio.Contracts;

/// <summary>Limits of the library folders, search and library info (backend-api 1.22.0, feature 022, data-model 6).</summary>
public static partial class ContractLimits
{
    /// <summary>Longest artist, album or song name for filing (library info and placement display names).</summary>
    public const int LibraryNameMaxLength = 200;

    /// <summary>Longest search text after trimming (<c>q</c>; longer input is rejected with <c>422 library-query-invalid</c>; the UI cuts in the box).</summary>
    public const int LibrarySearchMaxLength = 100;

    /// <summary>Most words of one search.</summary>
    public const int LibrarySearchMaxTokens = 8;

    /// <summary>Default hits per search group (<c>limit</c>).</summary>
    public const int LibrarySearchGroupDefault = 20;

    /// <summary>Most hits per search group (<c>limit</c>).</summary>
    public const int LibrarySearchGroupMax = 200;

    /// <summary>Default child folders per page (<c>limit</c> of the folder view).</summary>
    public const int LibraryFolderPageDefault = 200;

    /// <summary>Most child folders per page (<c>limit</c> of the folder view).</summary>
    public const int LibraryFolderPageMax = 500;

    /// <summary>Most items of a song folder in one view.</summary>
    public const int LibrarySongItemsMax = 500;

    /// <summary>Most album covers in an artist mosaic.</summary>
    public const int FolderArtMosaicMax = 4;

    /// <summary>Number of placeholder art tones (design-system tokens 0..7).</summary>
    public const int ArtToneCount = 8;

    /// <summary>Most entries of a breadcrumb (artist, album, song parents; the root is not listed).</summary>
    public const int LibraryBreadcrumbMax = 3;

    /// <summary>Most track ids of a song folder.</summary>
    public const int FolderTrackIdsMax = 10;

    /// <summary>Most highlights of one search hit.</summary>
    public const int SearchHighlightsMax = 32;

    /// <summary>Most matched instrument labels of one item hit.</summary>
    public const int MatchedInstrumentsMax = 8;

    /// <summary>Longest instrument label.</summary>
    public const int InstrumentLabelMaxLength = 60;

    /// <summary>Lowest album year.</summary>
    public const int LibraryYearMin = 1000;

    /// <summary>Highest album year.</summary>
    public const int LibraryYearMax = 2999;

    /// <summary>Pattern of a folder id: level prefix (<c>ar</c>, <c>al</c>, <c>so</c>) and 16 lower-case hex characters.</summary>
    public const string FolderIdPattern = "^(ar|al|so)[0-9a-f]{16}$";

    /// <summary>Pattern of a library revision (16 lower-case hex characters).</summary>
    public const string LibraryRevisionPattern = "^[0-9a-f]{16}$";
}
