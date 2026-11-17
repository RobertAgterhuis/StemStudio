namespace StemStudio.Contracts.Recording;

/// <summary>Body of <c>deleteRecordingTakes</c> (backend-api 1.14.0, feature 015).</summary>
public sealed record DeleteTakesRequest
{
    /// <summary>1..200 distinct take ids; unknown ids and ids of other tracks are ignored.</summary>
    public required IReadOnlyList<Guid> TakeIds { get; init; }
}
