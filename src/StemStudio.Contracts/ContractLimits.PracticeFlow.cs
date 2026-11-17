namespace StemStudio.Contracts;

/// <summary>Limits of the practice flow that are part of the contract (backend-api 1.13.0, feature 014, data-model 3).</summary>
public static partial class ContractLimits
{
    /// <summary>Most sections per track.</summary>
    public const int MaxSections = 64;

    /// <summary>Shortest section in seconds.</summary>
    public const double MinSectionSeconds = 1.0;

    /// <summary>Longest section label (after trimming).</summary>
    public const int MaxSectionLabel = 40;

    /// <summary>Most entries in one setlist.</summary>
    public const int MaxSetlistEntries = 100;

    /// <summary>Longest setlist name (after trimming).</summary>
    public const int MaxSetlistName = 80;

    /// <summary>Longest setlist note.</summary>
    public const int MaxSetlistNote = 2000;

    /// <summary>Longest setlist entry note.</summary>
    public const int MaxEntryNote = 1000;

    /// <summary>Most lyrics characters per song in a setlist PDF (longer lyrics are cut and marked "Lyrics shortened").</summary>
    public const int MaxLyricsCharsPerSong = 10_000;

    /// <summary>Longest practice session in hours.</summary>
    public const int MaxSessionHours = 12;

    /// <summary>Longest played time of a session in seconds (12 h).</summary>
    public const double MaxSessionPlayedSeconds = 43_200;

    /// <summary>How far a session start may lie in the future (clock skew), in minutes.</summary>
    public const int MaxSessionFutureMinutes = 5;

    /// <summary>Lowest browser offset from UTC in minutes (<c>utcOffsetMinutes</c>).</summary>
    public const int MinUtcOffsetMinutes = -840;

    /// <summary>Highest browser offset from UTC in minutes (<c>utcOffsetMinutes</c>).</summary>
    public const int MaxUtcOffsetMinutes = 840;

    /// <summary>Smallest summary window in days.</summary>
    public const int MinSummaryDays = 1;

    /// <summary>Largest summary window in days.</summary>
    public const int MaxSummaryDays = 365;

    /// <summary>Default summary window in days.</summary>
    public const int DefaultSummaryDays = 30;

    /// <summary>Most pages of one score view.</summary>
    public const int MaxScoreViewPages = 200;

    /// <summary>Most bars of one score view.</summary>
    public const int MaxScoreViewBars = 2000;
}
