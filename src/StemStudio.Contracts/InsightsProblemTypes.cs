namespace StemStudio.Contracts;

/// <summary>
/// Problem <c>type</c> codes of the quality report and the handouts (backend-api 1.19.0, feature 020); the full type is
/// <see cref="PracticeFlowProblemTypes.BaseUri"/> + code. <c>track-not-ready</c> and <c>setlist-empty</c> are the 014 codes.
/// </summary>
public static class InsightsProblemTypes
{
    /// <summary>409: a quality report of the track is already queued or running.</summary>
    public const string QualityRunning = "quality-running";

    /// <summary>409: the setlist has more than <see cref="ContractLimits.MaxSetlistHandoutSongs"/> entries (options and create).</summary>
    public const string TooManySongs = "too-many-songs";

    /// <summary>409: the song or setlist already has the most handouts that are being made (3).</summary>
    public const string HandoutsActive = "handouts-active";
}
