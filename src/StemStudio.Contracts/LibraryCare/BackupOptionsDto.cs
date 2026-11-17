namespace StemStudio.Contracts.LibraryCare;

/// <summary>Optional categories of a backup (<c>BackupOptions</c>); database, stems, artwork and pending uploads are always included. Every option defaults to true (backend-api 1.12.0, feature 013).</summary>
public sealed record BackupOptionsDto
{
    /// <summary>Include detail stems.</summary>
    public bool DetailStems { get; init; } = true;

    /// <summary>Include audio exports and stem packs.</summary>
    public bool Exports { get; init; } = true;

    /// <summary>Include sheet music items.</summary>
    public bool Scores { get; init; } = true;

    /// <summary>Include generated takes and converted vocals.</summary>
    public bool Generations { get; init; } = true;

    /// <summary>Include play-along takes (feature 015; empty before 015).</summary>
    public bool Recordings { get; init; } = true;

    /// <summary>Include voice profiles with samples and consent.</summary>
    public bool VoiceProfiles { get; init; } = true;
}
