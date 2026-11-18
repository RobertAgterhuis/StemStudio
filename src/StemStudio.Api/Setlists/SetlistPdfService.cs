using System.Text;
using Microsoft.Extensions.Options;
using StemStudio.Api.Practice;
using StemStudio.Api.SongSections;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Setlists;
using StemStudio.Domain.Sections;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Setlists;

/// <summary>
/// <c>GET /api/setlists/{id}/pdf</c> (014 FR-018): builds the content, runs the worker's setlist-document job, waits for it
/// (poll every 200 ms, at most <c>StemStudio:Practice:SetlistPdfTimeoutSeconds</c>), returns the PDF and deletes the job.
/// </summary>
/// <param name="setlists">Setlists.</param>
/// <param name="service">The setlist service (library facts).</param>
/// <param name="sections">Section sets.</param>
/// <param name="worker">The worker practice lane.</param>
/// <param name="options">Practice options.</param>
/// <param name="clock">Clock.</param>
public sealed class SetlistPdfService(
    ISetlistRepository setlists, SetlistService service, ISectionRepository sections, IWorkerPracticeClient worker, IOptions<PracticeFlowOptions> options, TimeProvider clock)
{
    /// <summary>Poll interval of the document job.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Detail of <c>document-failed</c> when the PDF is above <see cref="WorkerClient.MaxSetlistDocumentBytes"/>.</summary>
    public const string TooLarge = "The setlist PDF is too large; leave out the lyrics or split the setlist.";

    /// <summary>The PDF of a setlist.</summary>
    /// <param name="id">Setlist id.</param>
    /// <param name="pdfOptions">Print options from the query.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 PDF, 404, 409 <c>setlist-empty</c>, 503 <c>worker-unavailable</c> or <c>document-failed</c>, or 504 <c>document-timeout</c>.</returns>
    public async Task<IResult> PdfAsync(Guid id, SetlistPdfOptions pdfOptions, CancellationToken cancellationToken)
    {
        if (await setlists.FindAsync(id, cancellationToken) is not { } setlist)
        {
            return SetlistService.NotFound(id);
        }

        var library = await service.LibraryAsync(setlist, cancellationToken);
        if (!setlist.Entries.Any(entry => library.Tracks.ContainsKey(entry.TrackId)))
        {
            return PracticeFlowProblems.Conflict(PracticeFlowProblemTypes.SetlistEmpty, "The setlist has no songs", "Add songs before you make the PDF.");
        }

        var content = SetlistContentBuilder.Build(new SetlistSource(setlist, library, await SectionsAsync(library, cancellationToken)), pdfOptions, clock.GetUtcNow());
        return await RenderAsync(content, FileName(setlist.Name), cancellationToken);
    }

    /// <summary>A safe download name: letters, digits, space, dot, dash and underscore; at most 80 characters; "setlist" when empty.</summary>
    /// <param name="name">Setlist name.</param>
    /// <returns>The file name with <c>.pdf</c>.</returns>
    public static string FileName(string name)
    {
        var safe = new StringBuilder();
        foreach (var character in name ?? string.Empty)
        {
            safe.Append(char.IsLetterOrDigit(character) || character is ' ' or '-' or '_' or '.' ? character : '_');
        }

        var trimmed = safe.ToString().Trim(' ', '.', '_');
        trimmed = trimmed.Length > 80 ? trimmed[..80] : trimmed;
        return (trimmed.Length == 0 ? "setlist" : trimmed) + ".pdf";
    }

    private async Task<IResult> RenderAsync(WorkerSetlistDocumentRequest content, string fileName, CancellationToken cancellationToken)
    {
        string jobId;
        try
        {
            jobId = (await worker.CreateSetlistDocumentJobAsync(content, cancellationToken)).JobId;
        }
        catch (Exception exception) when (ProblemOf(exception) is { } problem)
        {
            return problem;
        }

        try
        {
            return await WaitAndDownloadAsync(jobId, fileName, cancellationToken);
        }
        finally
        {
            await DeleteQuietlyAsync(jobId);
        }
    }

    private async Task<IResult> WaitAndDownloadAsync(string jobId, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var job = await WaitAsync(jobId, cancellationToken);
            if (job?.Status != WorkerPracticeJobState.Completed)
            {
                return job is null ? Timeout() : DocumentFailed(job.Error ?? "The setlist PDF could not be made.");
            }

            using var pdf = new MemoryStream();
            await worker.DownloadSetlistDocumentAsync(jobId, pdf, cancellationToken);
            return TypedResults.File(pdf.ToArray(), "application/pdf", fileName);
        }
        catch (Exception exception) when (ProblemOf(exception) is { } problem)
        {
            return problem;
        }
    }

    /// <summary>The problem of a worker failure: unreachable, refused (4xx) or a PDF above the cap; null for anything else.</summary>
    private static IResult? ProblemOf(Exception exception) => exception switch
    {
        WorkerUnavailableException => PracticeFlowProblems.WorkerUnavailable(),
        WorkerRequestException rejected => DocumentFailed(rejected.Detail),
        WorkerResponseTooLargeException => DocumentFailed(TooLarge),
        _ => null,
    };

    /// <summary>The finished job, or null on timeout.</summary>
    private async Task<WorkerSetlistDocumentJob?> WaitAsync(string jobId, CancellationToken cancellationToken)
    {
        var deadline = clock.GetUtcNow() + options.Value.SetlistPdfTimeout;
        while (true)
        {
            var job = await worker.GetSetlistDocumentJobAsync(jobId, cancellationToken);
            if (job is { Status: WorkerPracticeJobState.Completed or WorkerPracticeJobState.Failed or WorkerPracticeJobState.Cancelled })
            {
                return job;
            }

            if (clock.GetUtcNow() >= deadline)
            {
                return null;
            }

            await Task.Delay(PollInterval, clock, cancellationToken);
        }
    }

    private async Task DeleteQuietlyAsync(string jobId)
    {
        try
        {
            await worker.DeleteSetlistDocumentJobAsync(jobId, CancellationToken.None);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            // The worker removes finished jobs after 24 h itself.
        }
    }

    private static IResult DocumentFailed(string detail) =>
        PracticeFlowProblems.Typed(StatusCodes.Status503ServiceUnavailable, PracticeFlowProblemTypes.DocumentFailed, "The PDF could not be made", detail);

    private static IResult Timeout() => PracticeFlowProblems.Typed(
        StatusCodes.Status504GatewayTimeout, PracticeFlowProblemTypes.DocumentTimeout, "The PDF took too long", "Try again in a moment.");

    private async Task<IReadOnlyDictionary<Guid, TrackSections>> SectionsAsync(SetlistLibrary library, CancellationToken cancellationToken)
    {
        var found = new Dictionary<Guid, TrackSections>();
        foreach (var trackId in library.Tracks.Keys)
        {
            if (await sections.FindAsync(trackId, cancellationToken) is { } set)
            {
                found[trackId] = set;
            }
        }

        return found;
    }
}
