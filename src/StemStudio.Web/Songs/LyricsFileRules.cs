using StemStudio.Contracts;

namespace StemStudio.Web.Songs;

/// <summary>Checks a lyrics file before it is uploaded (010 FR-001): <c>.txt</c>, <c>.lrc</c> or <c>.md</c>, at most 64 KiB.</summary>
public static class LyricsFileRules
{
    /// <summary>The <c>accept</c> attribute of the file input.</summary>
    public static string Accept => string.Join(',', ContractLimits.LyricsUploadExtensions);

    /// <summary>Why the file cannot be uploaded, or null.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The readable problem.</returns>
    public static string? Problem(string fileName, long size) =>
        !ContractLimits.LyricsUploadExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()) ? "Choose a .txt, .lrc or .md file."
        : size > ContractLimits.MaxLyricsUploadBytes ? "Lyrics files can be at most 64 KB."
        : null;
}
