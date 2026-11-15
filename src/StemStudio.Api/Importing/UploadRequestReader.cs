using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace StemStudio.Api.Importing;

/// <summary>Reads the <c>file</c> part of a multipart request as a stream (no form buffering, U4/G1).</summary>
public static class UploadRequestReader
{
    private const string NotMultipart = "Send the file as multipart/form-data with a part named 'file'.";
    private const string MissingPart = "Add a part named 'file' with an MP3, WAV or FLAC file.";
    private const string Incomplete = "The upload was incomplete. Try again.";

    /// <summary>Finds the <c>file</c> part and hands its body stream and file name to <paramref name="handleFile"/>.</summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="handleFile">Consumes the part body and the client file name.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The handler's result, or a 400/413 problem.</returns>
    public static async Task<IResult> ReadAsync(HttpRequest request, Func<Stream, string, Task<IResult>> handleFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var boundary = Boundary(request.ContentType);
        return boundary is null
            ? ApiProblems.Validation("file", NotMultipart)
            : await ReadMultipartAsync(new MultipartReader(boundary, request.Body), handleFile, cancellationToken);
    }

    private static async Task<IResult> ReadMultipartAsync(MultipartReader reader, Func<Stream, string, Task<IResult>> handleFile, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadSectionsAsync(reader, handleFile, cancellationToken);
        }
        catch (BadHttpRequestException exception)
        {
            return exception.StatusCode == StatusCodes.Status413PayloadTooLarge ? ApiProblems.TooLarge() : ApiProblems.Validation("file", Incomplete);
        }
    }

    /// <summary>The boundary of a multipart/form-data content type, or null.</summary>
    /// <param name="contentType">The request content type.</param>
    /// <returns>The boundary or null.</returns>
    internal static string? Boundary(string? contentType)
    {
        if (!MediaTypeHeaderValue.TryParse(contentType, out var media) || !media.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var boundary = HeaderUtilities.RemoveQuotes(media.Boundary).Value;
        return string.IsNullOrWhiteSpace(boundary) ? null : boundary;
    }

    private static async Task<IResult> ReadSectionsAsync(MultipartReader reader, Func<Stream, string, Task<IResult>> handleFile, CancellationToken cancellationToken)
    {
        while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
        {
            var fileName = FileName(section);
            if (fileName is not null)
            {
                return await handleFile(section.Body, fileName);
            }
        }

        return ApiProblems.Validation("file", MissingPart);
    }

    /// <summary>The client file name of the part named <c>file</c>, or null for any other part.</summary>
    /// <param name="section">The multipart section.</param>
    /// <returns>The file name or null.</returns>
    internal static string? FileName(MultipartSection section)
    {
        if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
            || !disposition.IsFileDisposition()
            || HeaderUtilities.RemoveQuotes(disposition.Name).Value != "file")
        {
            return null;
        }

        var name = disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName;
        return HeaderUtilities.RemoveQuotes(name).ToString();
    }
}
