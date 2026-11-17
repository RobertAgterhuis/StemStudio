using StemStudio.Domain.Mixes;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Mixes;

/// <summary>
/// The references a mix holds to other rows of its track: the loop's section (014 FR-005) and the take channel (015
/// data-model 2.6). An id the track does not have (deleted, or of another track) is dropped without error; the loop and the
/// other settings stay.
/// </summary>
/// <param name="sections">The track sections (feature 014).</param>
/// <param name="takes">The takes (feature 015).</param>
public sealed class MixReferences(ISectionRepository sections, IRecordingTakeRepository takes)
{
    /// <summary>The settings with only references the track has.</summary>
    /// <param name="trackId">The mix's track.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The cleaned settings (the same instance when nothing changed).</returns>
    public async Task<MixSettings> CleanAsync(Guid trackId, MixSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var loop = settings.Loop is { SectionId: { } sectionId } region && !await HasSectionAsync(trackId, sectionId, cancellationToken)
            ? region with { SectionId = null }
            : settings.Loop;
        var take = settings.Take is { } channel && !(await takes.IdsOfTrackAsync(trackId, cancellationToken)).Contains(channel.TakeId) ? null : settings.Take;
        return loop == settings.Loop && take == settings.Take ? settings : settings with { Loop = loop, Take = take };
    }

    private async Task<bool> HasSectionAsync(Guid trackId, Guid sectionId, CancellationToken cancellationToken) =>
        (await sections.FindAsync(trackId, cancellationToken))?.Sections.Any(section => section.Id == sectionId) == true;
}
