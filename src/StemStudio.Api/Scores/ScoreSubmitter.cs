using StemStudio.Api.Worker;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Scores;

/// <summary>Submits the transcription of a score item (<c>POST /v1/transcription-jobs</c>) and records the job.</summary>
/// <param name="worker">Worker client.</param>
/// <param name="clock">Clock.</param>
public sealed class ScoreSubmitter(IWorkerClient worker, TimeProvider clock)
{
    /// <summary>Longest title, subtitle and part name.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Current time (UTC).</summary>
    public DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Starts the transcription; null on success, else the problem (503 unreachable, 409 <c>source-unavailable</c>).</summary>
    /// <param name="asset">A queued score item.</param>
    /// <param name="draft">The plan.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Null or the problem.</returns>
    public async Task<IResult?> SubmitAsync(Asset asset, ScoreDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        try
        {
            asset.AssignJob((await worker.CreateTranscriptionJobAsync(Request(asset.Name, draft), cancellationToken)).TranscriptionJobId, Now);
            return null;
        }
        catch (Exception exception) when (ScoreProblems.Submission(exception) is { } problem)
        {
            return problem;
        }
    }

    /// <summary>The worker request of a draft (data-model 2.8, 4.4).</summary>
    /// <param name="name">The item name (the score title).</param>
    /// <param name="draft">The plan.</param>
    /// <returns>The request.</returns>
    public static WorkerTranscriptionJobRequest Request(string name, ScoreDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var (recipe, source) = (draft.Recipe, draft.Source);
        var drums = recipe.Profile == ScoreProfile.Drums;
        return new WorkerTranscriptionJobRequest
        {
            Profile = recipe.Profile,
            Quality = recipe.Quality ?? ScoreQuality.Accurate,
            Input = drums ? null : new WorkerTranscriptionInput(source.InputPath),
            DrumInputs = drums ? DrumInputs(source.DrumPieces) : null,
            Range = recipe.Range is { } range ? new WorkerRenderRange(range.StartSeconds, range.EndSeconds) : null,
            Grid = Grid(draft.Grid),
            Resolution = recipe.Resolution,
            KeyOverride = recipe.KeyOverride,
            Titles = new WorkerScoreTitles(Cut(name)!, Cut(source.TrackArtist is { } artist ? $"{source.TrackTitle} - {artist}" : source.TrackTitle), ProfileCatalog.InstrumentName(recipe.Profile)),
        };
    }

    private static WorkerDrumInputs DrumInputs(IReadOnlyDictionary<string, string> pieces) =>
        new(pieces.GetValueOrDefault("kick"), pieces.GetValueOrDefault("snare"), pieces.GetValueOrDefault("toms"), pieces.GetValueOrDefault("hihat"), pieces.GetValueOrDefault("cymbals"));

    private static object Grid(TempoGrid grid) => grid switch
    {
        BeatGridTempo beats => new WorkerBeatGridTempo(beats.Bpm, beats.BeatsPerBar, beats.Beats, beats.Downbeats),
        ManualTempoGrid manual => new WorkerManualTempo(manual.Bpm, manual.BeatsPerBar, manual.FirstDownbeatSeconds),
        _ => throw new ArgumentException("Unknown grid.", nameof(grid)),
    };

    private static string? Cut(string? value) => value is null || value.Length <= MaxTitleLength ? value : value[..MaxTitleLength];
}
