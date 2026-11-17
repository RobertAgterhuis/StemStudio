namespace StemStudio.Contracts.Sections;

/// <summary>Body of <c>replaceSections</c> (<c>SectionsUpdate</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record SectionsUpdateRequest
{
    /// <summary>The revision the edit started from (409 <c>sections-changed</c> otherwise).</summary>
    public required int Revision { get; init; }

    /// <summary>The whole ordered list, 1..64 sections.</summary>
    public required IReadOnlyList<SectionInputDto> Sections { get; init; }
}
