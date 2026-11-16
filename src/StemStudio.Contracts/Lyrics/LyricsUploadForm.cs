namespace StemStudio.Contracts.Lyrics;

/// <summary>Multipart field names of <c>POST /api/lyrics/upload</c> (backend contract 1.9.0).</summary>
public static class LyricsUploadForm
{
    /// <summary>The <c>.txt</c>, <c>.lrc</c> or <c>.md</c> file (UTF-8, at most 64 KiB).</summary>
    public const string File = "file";

    /// <summary>Optional title (default the file name without extension).</summary>
    public const string Title = "title";

    /// <summary>Optional language code (default <c>unknown</c>).</summary>
    public const string Language = "language";
}
