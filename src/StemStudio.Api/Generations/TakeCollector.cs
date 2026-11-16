using System.Globalization;
using StemStudio.Api.Generator;
using StemStudio.Domain.Generations;

namespace StemStudio.Api.Generations;

/// <summary>Picks the service of a generation: voice conversions run on the singer, the rest on the generator (010 ADR-019).</summary>
/// <param name="generator">The generator client.</param>
/// <param name="singer">The singer client.</param>
public sealed class GeneratorRouter(IGeneratorClient generator, ISingerClient singer)
{
    /// <summary>The client of a generation.</summary>
    /// <param name="generation">The generation.</param>
    /// <returns>The client.</returns>
    public IGeneratorClient For(Generation generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        return generation.Kind == GenerationKind.VoiceConversion ? singer : generator;
    }
}

/// <summary>
/// Completes a generation from its finished generator job: downloads every take (and the converted vocal of a
/// conversion) to <c>generations/&lt;id&gt;/</c>, verifies the SHA-256, registers each take in the library and
/// deletes the service's copy.
/// </summary>
/// <param name="router">Picks the service.</param>
/// <param name="storage">Take storage.</param>
/// <param name="library">The library port.</param>
/// <param name="clock">Clock.</param>
public sealed class TakeCollector(GeneratorRouter router, TakeStorage storage, IGeneratedAudioLibrary library, TimeProvider clock)
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
        var source = new JobSource(router.For(generation), job.GenerationId, generation);
        if (await DownloadAllAsync(source, job, cancellationToken))
        {
            await RegisterAsync(generation, cancellationToken);
            await DeleteCopyAsync(source, cancellationToken);
        }
    }

    private async Task<bool> DownloadAllAsync(JobSource source, GeneratorJob job, CancellationToken cancellationToken)
    {
        try
        {
            var takes = new List<GenerationTake>();
            foreach (var take in job.Takes)
            {
                takes.Add(await DownloadAsync(source, take, cancellationToken));
            }

            source.Generation.Complete(takes, Now);
            return true;
        }
        catch (Exception exception) when (exception is GeneratorUnavailableException or GeneratorRequestException or ArgumentException)
        {
            storage.DeleteAll(source.Generation.Id);
            FailIfDamaged(source.Generation, exception);
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

    private async Task<GenerationTake> DownloadAsync(JobSource source, GeneratorTake take, CancellationToken cancellationToken)
    {
        var path = TakeStorage.RelativePath(source.Generation.Id, take.Index);
        var size = await storage.SaveAsync(
            path, (stream, ct) => source.Client.DownloadTakeAsync(new GeneratorTakeRef(source.JobId, take.Index, take.Sha256), stream, ct), cancellationToken);
        var stored = new GenerationTake(take.Index, take.Seed, take.DurationSeconds, take.Sha256, size, take.PeakDbfs, path, new TakeModel(take.Model.Id, take.Model.Revision))
        {
            Vocal = Enum.TryParse<VocalLabel>(take.Vocal, true, out var vocal) ? vocal : null,
            VoiceProfileName = source.Generation.VoiceConversion?.VoiceProfileName,
        };
        return take.HasVocalStem ? await WithVocalAsync(source, stored, cancellationToken) : stored;
    }

    private async Task<GenerationTake> WithVocalAsync(JobSource source, GenerationTake take, CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"generations/{source.Generation.Id:D}/vocal-{take.Index}.flac");
        await storage.SaveAsync(path, (stream, ct) => source.Client.DownloadTakeVocalAsync(source.JobId, take.Index, stream, ct), cancellationToken);
        return take with { VocalFilePath = path };
    }

    private async Task RegisterAsync(Generation generation, CancellationToken cancellationToken)
    {
        foreach (var take in generation.Takes.ToList())
        {
            var entry = new GeneratedAudioEntry(storage.FullPath(take.FilePath), GenerationText.TakeTitle(generation, take.Index), generation, take);
            generation.SetLibraryAsset(take.Index, await library.RegisterAsync(entry, cancellationToken), Now);
        }
    }

    private static async Task DeleteCopyAsync(JobSource source, CancellationToken cancellationToken)
    {
        try
        {
            await source.Client.DeleteAsync(source.JobId, cancellationToken);
        }
        catch (GeneratorUnavailableException)
        {
            // Best effort: the service removes finished jobs after 24 hours.
        }
    }

    private sealed record JobSource(IGeneratorClient Client, string JobId, Generation Generation);
}
