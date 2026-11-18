namespace StemStudio.Contracts.Sections;

/// <summary>Body of <c>detectSections</c> (backend-api 1.13.0, feature 014).</summary>
public sealed record DetectSectionsRequest
{
    /// <summary>Replace edited sections (otherwise 409 <c>sections-edited</c>); default false.</summary>
    public bool Overwrite { get; init; }
}
