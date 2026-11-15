using System.Net;
using StemStudio.Api.Tracks;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Domain;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Artwork;

/// <summary>
/// Sets and resets the user's own picture (FR-013): the upload is forwarded to the worker for re-encoding (EXIF and
/// every other metadata block removed), the normalised JPEGs are stored in <c>artwork/user/</c>.
/// </summary>
/// <param name="tracks">Track repository.</param>
/// <param name="worker">Worker client.</param>
/// <param name="files">Picture store.</param>
/// <param name="clock">Clock.</param>
public sealed class UserArtworkService(ITrackRepository tracks, IWorkerClient worker, IArtworkFileStore files, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Normalises the uploaded picture and shows it.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="request">The multipart request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400, 404, 413, 415, 422 or 503.</returns>
    public async Task<IResult> SetAsync(Guid trackId, HttpRequest request, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(trackId);
        }

        var (part, problem) = await ArtworkUploadReader.ReadAsync(request, cancellationToken);
        return problem ?? await TryStoreAsync(track, part!, cancellationToken) ?? await SaveAsync(track, cancellationToken);
    }

    /// <summary>Removes the own picture and shows the automatic one (or none); idempotent.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> ResetAsync(Guid trackId, CancellationToken cancellationToken)
    {
        var track = await tracks.FindAsync(trackId, cancellationToken);
        if (track is null)
        {
            return TrackService.NotFound(trackId);
        }

        if (track.ResetArtwork(Now))
        {
            await tracks.SaveAsync(cancellationToken);
            files.Delete(trackId, ArtworkSlot.User);
        }

        return TypedResults.Ok(TrackMapper.ToDto(track));
    }

    /// <summary>The worker's rejection as the contract problem (title per status, the worker's detail).</summary>
    /// <param name="rejection">The rejection.</param>
    /// <returns>The problem.</returns>
    public static IResult Rejected(WorkerRequestException rejection)
    {
        ArgumentNullException.ThrowIfNull(rejection);
        var (status, title) = rejection.StatusCode switch
        {
            HttpStatusCode.RequestEntityTooLarge => (StatusCodes.Status413PayloadTooLarge, "Picture too large"),
            HttpStatusCode.UnsupportedMediaType => (StatusCodes.Status415UnsupportedMediaType, "Unsupported picture type"),
            HttpStatusCode.UnprocessableEntity => (StatusCodes.Status422UnprocessableEntity, "Picture too large"),
            _ => (StatusCodes.Status400BadRequest, "Not an image"),
        };
        return TypedResults.Problem(rejection.Detail, statusCode: status, title: title);
    }

    private async Task<IResult?> TryStoreAsync(Track track, ArtworkUploadPart part, CancellationToken cancellationToken)
    {
        try
        {
            await StoreAsync(track, part, cancellationToken);
            return null;
        }
        catch (Exception exception) when (StoreProblem(exception) is { } problem)
        {
            return problem;
        }
    }

    private static IResult? StoreProblem(Exception exception) => exception switch
    {
        WorkerRequestException rejection => Rejected(rejection),
        WorkerUnavailableException or DomainValidationException => ApiProblems.Unavailable(),
        _ => null,
    };

    private async Task StoreAsync(Track track, ArtworkUploadPart part, CancellationToken cancellationToken)
    {
        using var content = new MemoryStream(part.Bytes, writable: false);
        var upload = await worker.NormalizeArtworkAsync(new WorkerArtworkFile(content, part.FileName, part.ContentType), cancellationToken);
        var candidate = new ArtworkCandidate(ArtworkSource.User, upload.Info.Sha256);
        var images = new ArtworkImages(await DownloadAsync(upload.ArtworkId, ApiRoutes.ArtworkCover, cancellationToken), await DownloadAsync(upload.ArtworkId, ApiRoutes.ArtworkThumb, cancellationToken));
        await files.SaveAsync(track.Id, ArtworkSlot.User, images, cancellationToken);
        track.SetUserArtwork(candidate.Sha256, Now);
    }

    private async Task<byte[]> DownloadAsync(string artworkId, string variant, CancellationToken cancellationToken) =>
        await worker.GetArtworkAsync(new WorkerArtworkRef(WorkerArtworkOwner.Upload, artworkId, variant), cancellationToken)
        ?? throw new WorkerUnavailableException("The normalised picture is no longer available in the processing service.");

    private async Task<IResult> SaveAsync(Track track, CancellationToken cancellationToken)
    {
        await tracks.SaveAsync(cancellationToken);
        return TypedResults.Ok(TrackMapper.ToDto(track));
    }
}
