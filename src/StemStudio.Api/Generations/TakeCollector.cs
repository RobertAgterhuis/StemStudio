using StemStudio.Api.Generator;
using StemStudio.Domain.Generations;

namespace StemStudio.Api.Generations;

/// <summary>
/// Completes a generation from its finished generator job: downloads every take to <c>generations/&lt;id&gt;/</c>,
/// verifies the SHA-256, registers each take in the library and deletes the generator's copy.
/// </summary>
/// <param name="generator">The generator client.</param>
/// <param name="storage">Take storage.</param>
/// <param name="library">The library port.</param>
/// <param name="clock">Clock.</param>
public sealed class TakeCollector(IGeneratorClient generator, TakeStorage storage, IGeneratedAudioLibrary library, TimeProvider clock)
{
    /// <summary>Error text when a take cannot be downloaded intact.</summary>
    public const string DamagedTake = "A take could not be downloaded intact. Try again.";

    /// <summary>Collects the takes; a damaged or missing take fails the generation; an outage leaves it for the next pass.</summary>
    /// <param name="generation">The generation (active).</param>
    /// <param name="job">The completed generator job.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task CollectAsync(Generation generation, GeneratorJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(job);
        if (await DownloadAllAsync(generation, job, cancellationToken))
        {
            await RegisterAsync(generation, cancellationToken);
            await DeleteCopyAsync(job.GenerationId, cancellationToken);
        }
    }

    private async Task<bool> DownloadAllAsync(Generation generation, GeneratorJob job, CancellationToken cancellationToken)
    {
        try
        {
            var takes = new List<GenerationTake>();
            foreach (var take in job.Takes)
            {
                takes.Add(await DownloadAsync(generation.Id, job.GenerationId, take, cancellationToken));
            }

            generation.Complete(takes, Now);
            return true;
        }
        catch (Exception exception) when (exception is GeneratorUnavailableException or GeneratorRequestException or ArgumentException)
        {
            storage.DeleteAll(generation.Id);
            FailIfDamaged(generation, exception);
            return false;
        }
    }

    private void FailIfDamaged(Generation generation, Exception exception)
    {
        if (exception is not GeneratorUnavailableException || exception is GeneratorIntegrityException)
        {
            generation.Fail(new GenerationError(GenerationErrorCode.EngineError, DamagedTake), Now);
        }
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task<GenerationTake> DownloadAsync(Guid generationId, string jobId, GeneratorTake take, CancellationToken cancellationToken)
    {
        var path = TakeStorage.RelativePath(generationId, take.Index);
        var size = await storage.SaveAsync(
            path, (stream, ct) => generator.DownloadTakeAsync(new GeneratorTakeRef(jobId, take.Index, take.Sha256), stream, ct), cancellationToken);
        return new GenerationTake(take.Index, take.Seed, take.DurationSeconds, take.Sha256, size, take.PeakDbfs, path, new TakeModel(take.Model.Id, take.Model.Revision));
    }

    private async Task RegisterAsync(Generation generation, CancellationToken cancellationToken)
    {
        foreach (var take in generation.Takes.ToList())
        {
            var entry = new GeneratedAudioEntry(storage.FullPath(take.FilePath), GenerationText.TakeTitle(generation, take.Index), generation, take);
            generation.SetLibraryAsset(take.Index, await library.RegisterAsync(entry, cancellationToken), Now);
        }
    }

    private async Task DeleteCopyAsync(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            await generator.DeleteAsync(jobId, cancellationToken);
        }
        catch (GeneratorUnavailableException)
        {
            // Best effort: the generator removes finished jobs after 24 hours.
        }
    }
}
