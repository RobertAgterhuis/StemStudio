namespace StemStudio.Contracts.LibraryCare;

/// <summary>Report of a completed verify job (<c>VerifyReport</c>, <c>type: verify</c>; backend-api 1.12.0, feature 013).</summary>
public sealed record VerifyReportDto : LibraryJobReportDto
{
    /// <summary>True when every entry matched its size and SHA-256.</summary>
    public required bool Ok { get; init; }

    /// <summary>Entries checked.</summary>
    public required int EntriesChecked { get; init; }

    /// <summary>Bytes checked.</summary>
    public required long BytesChecked { get; init; }

    /// <summary>The first problem found; null when <see cref="Ok"/>.</summary>
    public required LibraryJobErrorDto? Problem { get; init; }
}
