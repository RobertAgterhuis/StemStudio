using System.Globalization;
using StemStudio.Api.Generations;
using StemStudio.Api.Generator;
using StemStudio.Api.Storage;
using StemStudio.Domain.Generations;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Voices;

/// <summary>
/// Deletes a voice profile and every file derived from its sample (010 FR-009, SC-006): active conversions with the
/// profile are cancelled on the singer, <c>voices/&lt;id&gt;/</c> is removed, converted takes are deleted
/// (<c>deleteTakes=true</c>) or kept with the name snapshot "&lt;name&gt; (profile deleted)".
/// </summary>
/// <param name="profiles">Profile repository.</param>
/// <param name="generations">Generation repository.</param>
/// <param name="lifecycle">Deletes generations with their files and library items.</param>
/// <param name="router">Picks the singer.</param>
/// <param name="data">The data directory.</param>
/// <param name="clock">Clock.</param>
public sealed class VoiceProfileDeletion(
    IVoiceProfileRepository profiles, IGenerationRepository generations, GenerationService lifecycle, GeneratorRouter router, DataDirectory data, TimeProvider clock)
{
    /// <summary>Deletes the profile.</summary>
    /// <param name="id">Profile id.</param>
    /// <param name="deleteTakes">Also delete the converted takes.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public async Task<IResult> DeleteAsync(Guid id, bool deleteTakes, CancellationToken cancellationToken)
    {
        var profile = await profiles.FindAsync(id, cancellationToken);
        if (profile is null)
        {
            return VoiceProblems.NotFound(id);
        }

        foreach (var conversion in await generations.ListByVoiceProfileAsync(id, cancellationToken))
        {
            await (deleteTakes ? lifecycle.DeleteAsync(conversion.Id, cancellationToken) : DetachAsync(conversion, id, cancellationToken));
        }

        await generations.SaveAsync(cancellationToken);
        await profiles.RemoveAsync(profile, cancellationToken);
        DeleteFolder(id);
        return TypedResults.NoContent();
    }

    private async Task<IResult> DetachAsync(Generation conversion, Guid profileId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (conversion.IsActive)
        {
            await StopAsync(conversion, cancellationToken);
            conversion.Cancel(now);
        }

        conversion.DetachVoiceProfile(profileId, now);
        return TypedResults.NoContent();
    }

    private async Task StopAsync(Generation conversion, CancellationToken cancellationToken)
    {
        try
        {
            if (conversion.GeneratorJobId is { } job)
            {
                await router.For(conversion).DeleteAsync(job, cancellationToken);
            }
        }
        catch (GeneratorUnavailableException)
        {
            // The singer deletes its copies of the prompt when the job ends (or after 24 hours).
        }
    }

    private void DeleteFolder(Guid id)
    {
        var folder = Path.Combine(data.Sub("voices"), id.ToString("D", CultureInfo.InvariantCulture));
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
