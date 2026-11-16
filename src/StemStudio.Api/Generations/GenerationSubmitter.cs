using StemStudio.Api.Generator;
using StemStudio.Api.Storage;
using StemStudio.Domain.Generations;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Generations;

/// <summary>Sends a queued generation to the generator; on a refusal or outage the generation is removed again.</summary>
/// <param name="generator">The generator client.</param>
/// <param name="generations">The repository.</param>
/// <param name="stems">The stem store (paths of the source stems).</param>
/// <param name="availability">The availability cache (invalidated when the generator refuses for the GPU).</param>
/// <param name="clock">Clock.</param>
public sealed class GenerationSubmitter(
    IGeneratorClient generator, IGenerationRepository generations, IStemFileStore stems, GenerationAvailability availability, TimeProvider clock)
{
    /// <summary>Stores and submits a new generation.</summary>
    /// <param name="generation">The queued generation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Null on success, else the problem to answer (nothing is kept).</returns>
    public async Task<IResult?> AddAndSubmitAsync(Generation generation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(generation);
        await generations.AddAsync(generation, cancellationToken);
        try
        {
            var job = await SubmitJobAsync(generation, cancellationToken);
            generation.AssignJob(job.GenerationId, clock.GetUtcNow().UtcDateTime);
            await generations.SaveAsync(cancellationToken);
            return null;
        }
        catch (Exception exception) when (exception is GeneratorUnavailableException or GeneratorRequestException)
        {
            await generations.RemoveAsync(generation, CancellationToken.None);
            InvalidateOnGpuRefusal(exception);
            return GenerationProblems.Submission(exception);
        }
    }

    private void InvalidateOnGpuRefusal(Exception exception)
    {
        if (exception is GeneratorRequestException { Code: GeneratorProblemCode.GpuRequired })
        {
            availability.Invalidate();
        }
    }

    private Task<GeneratorJob> SubmitJobAsync(Generation generation, CancellationToken cancellationToken) => generation.Kind switch
    {
        GenerationKind.Song => generator.CreateSongAsync(SongParams(generation), cancellationToken),
        _ => generator.CreateRestyleAsync(Request(generation), cancellationToken),
    };

    private static GeneratorSongParams SongParams(Generation generation)
    {
        var song = generation.Song!;
        var lyrics = song.Instrumental ? "[Instrumental]" : song.Lyrics.Text;
        return new GeneratorSongParams(
            song.Caption, lyrics, song.Instrumental, song.Lyrics.Language, song.Bpm, song.KeyScale, song.TimeSignature, song.DurationSeconds, generation.Seeds);
    }

    private GeneratorRestyleRequest Request(Generation generation)
    {
        var details = generation.Restyle!;
        var trackId = details.SourceTrackId!.Value;
        var parameters = new GeneratorRestyleParams(
            details.Caption, details.AudioCoverStrength, Domain.Styles.StylePromptBuilder.InferenceSteps, Domain.Styles.StylePromptBuilder.Shift,
            generation.Seeds, details.Range.Mode, details.Range.StartSeconds, details.Range.EndSeconds);
        return new GeneratorRestyleRequest(parameters, details.IncludedStems.Select(stem => new GeneratorStemFile(stem, stems.GetStemPath(trackId, stem))).ToList());
    }
}
