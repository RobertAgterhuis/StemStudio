namespace StemStudio.Contracts.Recording;

/// <summary>Result of <c>deleteRecordingTakes</c> (backend-api 1.14.0, feature 015).</summary>
public sealed record DeleteTakesResultDto
{
    /// <summary>Takes deleted.</summary>
    public required int Deleted { get; init; }
}
