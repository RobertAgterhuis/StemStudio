using StemStudio.Api.Generations;
using StemStudio.Api.Generator;
using StemStudio.Api.Storage;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Voices;

/// <summary>
/// Moves a voice conversion out of <c>waitingForStems</c> (010 data-model 3.2): once the imported take is separated,
/// the <c>vocals</c> stem (target), the other stems (accompaniment) and the profile sample (prompt) go to the singer;
/// a failed separation fails the conversion; a singer outage leaves it waiting for the next pass.
/// </summary>
/// <param name="tracks">Track repository.</param>
/// <param name="profiles">Voice profiles.</param>
/// <param name="stems">Stem store.</param>
/// <param name="router">Picks the singer.</param>
/// <param name="data">The data directory.</param>
/// <param name="clock">Clock.</param>
public sealed class ConversionStemWaiter(
    ITrackRepository tracks, IVoiceProfileRepository profiles, IStemFileStore stems, GeneratorRouter router, DataDirectory data, TimeProvider clock)
{
    /// <summary>Error text when the take could not be separated.</summary>
    public const string NotSeparated = "The take could not be separated into stems.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Submits the conversion when its stems are ready.</summary>
    /// <param name="conversion">A conversion in <c>waitingForStems</c>.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task AdvanceAsync(Generation conversion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        var details = conversion.VoiceConversion!;
        var track = await tracks.FindAsync(details.ImportedTrackId!.Value, cancellationToken);
        var profile = details.VoiceProfileId is { } id ? await profiles.FindAsync(id, cancellationToken) : null;
        if (track is null || track.Status == TrackStatus.Failed || profile is null)
        {
            conversion.Fail(new GenerationError(GenerationErrorCode.EngineError, profile is null ? "The voice profile was deleted." : NotSeparated), Now);
            return;
        }

        if (track.Status == TrackStatus.Completed)
        {
            await SubmitAsync(conversion, new GeneratorVoiceConversionRequest(Params(details), Stem(track, "vocals"), SamplePath(profile.SamplePath), Accompaniment(track)), cancellationToken);
        }
    }

    private async Task SubmitAsync(Generation conversion, GeneratorVoiceConversionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var job = await router.For(conversion).CreateVoiceConversionAsync(request, cancellationToken);
            conversion.AssignJob(job.GenerationId, Now);
            conversion.ApplyReport(new GenerationReport(GenerationState.Queued, 0, "Queued", null, []), Now);
        }
        catch (GeneratorRequestException refused)
        {
            conversion.Fail(new GenerationError(GenerationErrorCode.EngineError, refused.Detail), Now);
        }
        catch (GeneratorUnavailableException)
        {
            // The singer is down: the conversion keeps waiting and is submitted on a later pass.
        }
    }

    private static GeneratorVoiceConversionParams Params(VoiceConversionDetails details) => new(details.PitchShift, details.AutoShift, details.VoiceProfileName);

    private string Stem(Track track, string name) => stems.GetStemPath(track.Id, name);

    private List<string> Accompaniment(Track track) => track.Stems.Select(stem => stem.Name).Where(name => name != "vocals").Select(name => Stem(track, name)).ToList();

    private string SamplePath(string relative) => Path.Combine(data.Root, relative.Replace('/', Path.DirectorySeparatorChar));
}
