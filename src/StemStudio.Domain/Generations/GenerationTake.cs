namespace StemStudio.Domain.Generations;

/// <summary>A stored take of a completed generation (FLAC 24-bit stereo 48 kHz, AI-labelled).</summary>
/// <param name="Index">Take number from 1.</param>
/// <param name="Seed">The seed.</param>
/// <param name="DurationSeconds">Duration.</param>
/// <param name="Sha256">SHA-256 of the file (64 lower-case hex characters).</param>
/// <param name="SizeBytes">File size.</param>
/// <param name="PeakDbfs">Sample peak.</param>
/// <param name="FilePath">Path relative to the API data directory.</param>
/// <param name="Model">The model that made it.</param>
public sealed record GenerationTake(int Index, int Seed, double DurationSeconds, string Sha256, long SizeBytes, double PeakDbfs, string FilePath, TakeModel Model)
{
    /// <summary>The take's library item (007), once registered.</summary>
    public Guid? LibraryAssetId { get; init; }

    /// <summary>The track it was imported as, if any.</summary>
    public Guid? ImportedTrackId { get; init; }

    /// <summary>Always true: every take is AI-generated (FR-008).</summary>
    public bool AiGenerated { get; } = true;
}
