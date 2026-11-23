#pragma warning disable IDE0005, CS9113, CS1998, IDE0060, CA1822, CA1801, IDE0051, IDE0052, CS0169, CS0414
using StemStudio.Api.Handouts.Sources;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Handouts;

/// <summary>Reads everything a song brings into a handout through the 014/016 source ports (data-model 6.3 step 1).</summary>
/// <param name="sections">014 sections.</param>
/// <param name="chords">016 chord chart.</param>
/// <param name="practice">014 practice log.</param>
/// <param name="notation">008/016 notation items.</param>
/// <param name="notes">Practice notes.</param>
public sealed class HandoutMaterialLoader(ISectionSource sections, IChordSource chords, IPracticeLogSource practice, INotationCatalog notation, IPracticeNoteRepository notes)
{
    /// <summary>The material of one song.</summary>
    /// <param name="track">The song (with its lyrics row).</param>
    /// <param name="family">The family (its practice note).</param>
    /// <param name="entryNotes">Setlist entry notes, or null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The material.</returns>
    public Task<HandoutMaterial> LoadAsync(Track track, HandoutFamily family, string? entryNotes, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <summary>The library path of the shown cover (<c>tracks/&lt;id&gt;/artwork/(auto|user)/cover.jpg</c>), or null.</summary>
    /// <param name="track">The track.</param>
    /// <returns>The path or null.</returns>
    public static string? ArtworkPath(Track track)
    {
        throw new NotImplementedException();
    }
}
