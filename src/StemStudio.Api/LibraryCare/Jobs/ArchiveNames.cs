using System.Text.RegularExpressions;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Api.LibraryCare.Jobs;

/// <summary>Archive file names (backup-archive 1.0.0 section 1): the only file names that ever reach a path.</summary>
public static partial class ArchiveNames
{
    /// <summary>True when the name matches <see cref="LibraryCareLimits.ArchiveFileNamePattern"/>.</summary>
    /// <param name="fileName">A candidate file name.</param>
    /// <returns>True when valid.</returns>
    public static bool IsValid(string? fileName) => fileName is not null && !fileName.EndsWith('\n') && Pattern().IsMatch(fileName);

    [GeneratedRegex(LibraryCareLimits.ArchiveFileNamePattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}
