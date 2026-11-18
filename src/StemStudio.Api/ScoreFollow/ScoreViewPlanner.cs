using StemStudio.Domain.Assets;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.ScoreFollow;

/// <summary>What a score view job needs and where its bars sit on the track.</summary>
/// <param name="TrackId">Source track of the score, or null.</param>
/// <param name="OffsetSeconds">Track time of the score's input start (008 range start + exported asset range start).</param>
/// <param name="DurationSeconds">Length of the score's input.</param>
/// <param name="Grid">The grid the score was made with (times relative to the input start), or null when none can be resolved.</param>
/// <param name="MusicXmlPath">Library path of the MusicXML file.</param>
public sealed record ScoreViewPlan(Guid? TrackId, double OffsetSeconds, double DurationSeconds, TempoGrid? Grid, string MusicXmlPath)
{
    /// <summary>Track time where the score ends.</summary>
    public double EndSeconds => OffsetSeconds + DurationSeconds;
}

/// <summary>The score asset and its sources (the track, and the exported audio item when the score was made from one).</summary>
/// <param name="Score">The score asset.</param>
/// <param name="Track">The source track, or null when it no longer exists.</param>
/// <param name="Audio">The source audio item, or null for a score made from a channel.</param>
public sealed record ScoreSources(Asset Score, Track? Track, Asset? Audio);

/// <summary>Resolves the inputs of a score view from the stored 008 recipe (data-model 3.5).</summary>
public static class ScoreViewPlanner
{
    /// <summary>Role of the MusicXML file of a score asset.</summary>
    public const string MusicXmlRole = "musicxml";

    private const double MaxInputSeconds = 900;

    /// <summary>The plan of a ready score, or null when it has no recipe or no MusicXML.</summary>
    /// <param name="sources">The score and its sources.</param>
    /// <returns>The plan, or null.</returns>
    public static ScoreViewPlan? Plan(ScoreSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var (score, track, audio) = sources;
        if (score.Provenance.ScoreRecipe is not { } recipe || score.Files.FirstOrDefault(file => file.Role == MusicXmlRole) is not { } musicXml)
        {
            return null;
        }

        var offset = TempoGridResolver.Offset(audio?.Provenance.Recipe?.Range?.StartSeconds, recipe.Range?.StartSeconds);
        var duration = recipe.Range is { } range ? range.EndSeconds - range.StartSeconds : InputLength(track, audio);
        var grid = TempoGridResolver.Resolve(track?.BeatGrid, recipe.Tempo, offset);
        return new ScoreViewPlan(TrackOf(sources), offset, Math.Clamp(duration, 1, MaxInputSeconds), grid, $"assets/{score.Id:D}/{musicXml.FileName}");
    }

    /// <summary>The track a score belongs to: its own provenance track, else the source audio item's track.</summary>
    /// <param name="sources">The score and its sources.</param>
    /// <returns>The track id, or null.</returns>
    public static Guid? TrackOf(ScoreSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return sources.Score.TrackId ?? sources.Score.Provenance.ScoreRecipe?.Source.TrackId ?? sources.Audio?.TrackId;
    }

    private static double InputLength(Track? track, Asset? audio) => audio?.Audio?.DurationSeconds ?? track?.DurationSeconds ?? 0;
}
