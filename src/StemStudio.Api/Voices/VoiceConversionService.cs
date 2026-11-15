using StemStudio.Api.Generations;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Domain.Generations;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Voices;

/// <summary>"Use my voice" (010 FR-008): converts the vocal of a completed song take to a voice profile.</summary>
/// <param name="generations">Generation repository.</param>
/// <param name="profiles">Voice profiles.</param>
/// <param name="availability">Singer availability.</param>
/// <param name="importer">Imports the take when needed.</param>
/// <param name="waiter">Submits once the stems are ready.</param>
/// <param name="clock">Clock.</param>
public sealed class VoiceConversionService(
    IGenerationRepository generations, IVoiceProfileRepository profiles, SingerAvailability availability, TakeImporter importer, ConversionStemWaiter waiter, TimeProvider clock)
{
    /// <summary>Creates the conversion: imports the take if needed and waits for its stems.</summary>
    /// <param name="generationId">The song generation.</param>
    /// <param name="index">The take number.</param>
    /// <param name="request">Profile and pitch options.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 400, 404, 409 or 503.</returns>
    public async Task<IResult> CreateAsync(Guid generationId, int index, VoiceConversionRequest? request, CancellationToken cancellationToken)
    {
        var source = await generations.FindAsync(generationId, cancellationToken);
        if (source?.Take(index) is not { } take)
        {
            return ApiProblems.NotFound($"Generation {generationId} has no take {index}.");
        }

        if (source.Kind != GenerationKind.Song || take.Vocal != VocalLabel.Synthetic)
        {
            return VoiceProblems.NoVocals();
        }

        if (request is null || request.PitchShift is < -12 or > 12)
        {
            return ApiProblems.Validation("pitchShift", "The pitch shift must be -12 to 12 semitones.");
        }

        return await WithProfileAsync(source, take, request, cancellationToken);
    }

    private async Task<IResult> WithProfileAsync(Generation source, GenerationTake take, VoiceConversionRequest request, CancellationToken cancellationToken)
    {
        var profile = await profiles.FindAsync(request.ProfileId, cancellationToken);
        if (profile is null)
        {
            return VoiceProblems.NotFound(request.ProfileId);
        }

        var readiness = await availability.GetAsync(cancellationToken);
        if (!readiness.IsReady)
        {
            return VoiceProblems.NotReady(readiness);
        }

        var details = new VoiceConversionDetails
        {
            SourceGenerationId = source.Id,
            SourceTakeIndex = take.Index,
            VoiceProfileId = profile.Id,
            VoiceProfileName = profile.DisplayName,
            PitchShift = request.PitchShift,
            AutoShift = request.AutoShift,
        };
        return await StartAsync(source, Generation.CreateVoiceConversion(Guid.NewGuid(), details, clock.GetUtcNow().UtcDateTime), cancellationToken);
    }

    private async Task<IResult> StartAsync(Generation source, Generation conversion, CancellationToken cancellationToken)
    {
        var index = conversion.VoiceConversion!.SourceTakeIndex;
        var trackId = source.Take(index)!.ImportedTrackId;
        if (trackId is null)
        {
            var (result, imported) = await importer.ImportTakeAsync(source, index, cancellationToken);
            if (imported is null)
            {
                return result;
            }

            trackId = imported;
        }

        conversion.WaitForStems(trackId.Value, clock.GetUtcNow().UtcDateTime);
        await waiter.AdvanceAsync(conversion, cancellationToken);
        await generations.AddAsync(conversion, cancellationToken);
        return TypedResults.Accepted(ApiRoutes.Generation(conversion.Id), GenerationMapper.ToDto(conversion));
    }
}
