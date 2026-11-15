namespace StemStudio.Api.Assets;

/// <summary><c>Content-Disposition</c> of an item download (RFC 6266 with the RFC 5987 <c>filename*</c> parameter).</summary>
public static class ContentDisposition
{
    /// <summary>The header value with an ASCII fallback name and the UTF-8 name.</summary>
    /// <param name="fileName">Sanitised download file name.</param>
    /// <param name="inline">True for <c>inline</c> (player), else <c>attachment</c>.</param>
    /// <returns>The header value.</returns>
    public static string Attachment(string fileName, bool inline)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var ascii = new string(fileName.Select(c => c is >= ' ' and <= '~' and not '"' and not '\\' ? c : '_').ToArray());
        return $"{(inline ? "inline" : "attachment")}; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }
}
