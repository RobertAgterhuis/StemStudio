using Microsoft.AspNetCore.WebUtilities;
using StemStudio.Api.Importing;
using StemStudio.Contracts;

namespace StemStudio.Api.Artwork;

/// <summary>The <c>file</c> part of an own-picture upload, read into memory (at most 10 MiB).</summary>
/// <param name="Bytes">The file content.</param>
/// <param name="FileName">Client file name (only forwarded to the worker).</param>
/// <param name="ContentType">Media type of the part, if sent.</param>
public sealed record ArtworkUploadPart(byte[] Bytes, string FileName, string? ContentType);

/// <summary>Reads the picture part of <c>PUT /api/tracks/{id}/artwork</c>; the 10 MiB limit is enforced while streaming.</summary>
public static class ArtworkUploadReader
{
    private const string NotMultipart = "Send the picture as multipart/form-data with a part named 'file'.";
    private const string MissingPart = "Add a part named 'file' with a JPEG, PNG or WebP picture.";
    private const string Incomplete = "The upload was incomplete. Try again.";

    /// <summary>Reads the part, or returns the 400/413 problem.</summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The part, or null with the problem.</returns>
    public static async Task<(ArtworkUploadPart? Part, IResult? Problem)> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var boundary = UploadRequestReader.Boundary(request.ContentType);
        if (boundary is null)
        {
            return (null, ApiProblems.Validation("file", NotMultipart));
        }

        return await ReadMultipartAsync(new MultipartReader(boundary, request.Body), cancellationToken);
    }

    /// <summary>413 problem for pictures over 10 MiB.</summary>
    /// <returns>The result.</returns>
    public static IResult TooLarge() => TypedResults.Problem(
        "The picture is larger than 10 MiB.", statusCode: StatusCodes.Status413PayloadTooLarge, title: "Picture too large");

    private static async Task<(ArtworkUploadPart? Part, IResult? Problem)> ReadMultipartAsync(MultipartReader reader, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadSectionsAsync(reader, cancellationToken);
        }
        catch (BadHttpRequestException)
        {
            return (null, ApiProblems.Validation("file", Incomplete));
        }
    }

    private static async Task<(ArtworkUploadPart?, IResult?)> ReadSectionsAsync(MultipartReader reader, CancellationToken cancellationToken)
    {
        while (await reader.ReadNextSectionAsync(cancellationToken) is { } section)
        {
            if (UploadRequestReader.FileName(section) is { } fileName)
            {
                var bytes = await ReadCappedAsync(section.Body, cancellationToken);
                return bytes is null ? (null, TooLarge()) : (new ArtworkUploadPart(bytes, fileName, section.ContentType), null);
            }
        }

        return (null, ApiProblems.Validation("file", MissingPart));
    }

    private static async Task<byte[]?> ReadCappedAsync(Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (await body.ReadAsync(chunk, cancellationToken) is var read and > 0)
        {
            if (buffer.Length + read > ContractLimits.MaxArtworkUploadBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
