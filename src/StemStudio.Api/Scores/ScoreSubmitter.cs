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
        var recipe = draft.Recipe;
        return WithInputs(draft, WithNotation(draft, new WorkerTranscriptionJobRequest
        {
            Profile = recipe.Profile,
            Quality = recipe.Quality ?? ScoreQuality.Accurate,
            Range = recipe.Range is { } range ? new WorkerRenderRange(range.StartSeconds, range.EndSeconds) : null,
            Grid = WorkerGrid(draft.Grid),
            Resolution = recipe.Resolution,
            KeyOverride = recipe.KeyOverride,
            Titles = Titles(name, draft.Source, recipe.Profile),
        }));
    }

    /// <summary>The 016 notation additions: tablature, part and chord symbols.</summary>
    private static WorkerTranscriptionJobRequest WithNotation(ScoreDraft draft, WorkerTranscriptionJobRequest request) => request with
    {
        Tablature = ScoreNotationWire.ToWire(draft.Recipe.Tablature),
        Part = ScoreNotationWire.ToWire(draft.Recipe.Part),
        Chords = ScoreNotationWire.Chords(draft.Chords),
        PreferFlats = draft.Chords?.PreferFlats,
    };

    /// <summary>Pitched profiles read one input, drums the kept drum pieces (data-model 4.4).</summary>
    private static WorkerTranscriptionJobRequest WithInputs(ScoreDraft draft, WorkerTranscriptionJobRequest request) => draft.Recipe.Profile == ScoreProfile.Drums
        ? request with { DrumInputs = DrumInputs(draft.Source.DrumPieces), SinglePiece = ProfileCatalog.DrumPiece(draft.Recipe.Source.Channel) }
        : request with { Input = new WorkerTranscriptionInput(draft.Source.InputPath) };

    private static WorkerScoreTitles Titles(string name, ResolvedSource source, ScoreProfile profile) =>
        new(Cut(name)!, Cut(source.TrackArtist is { } artist ? $"{source.TrackTitle} - {artist}" : source.TrackTitle), ProfileCatalog.InstrumentName(profile));

    private static WorkerDrumInputs DrumInputs(IReadOnlyDictionary<string, string> pieces) =>
        new(pieces.GetValueOrDefault("kick"), pieces.GetValueOrDefault("snare"), pieces.GetValueOrDefault("toms"), pieces.GetValueOrDefault("hihat"), pieces.GetValueOrDefault("cymbals"));

    /// <summary>The worker grid of a resolved tempo grid (also used by the 014 score views).</summary>
    /// <param name="grid">A beat-grid or a manual tempo.</param>
    /// <returns>The worker body (<see cref="WorkerBeatGridTempo"/> or <see cref="WorkerManualTempo"/>), serialised by its runtime type.</returns>
    public static object WorkerGrid(TempoGrid grid) => grid switch
    {
        BeatGridTempo beats => new WorkerBeatGridTempo(beats.Bpm, beats.BeatsPerBar, beats.Beats, beats.Downbeats),
        ManualTempoGrid manual => new WorkerManualTempo(manual.Bpm, manual.BeatsPerBar, manual.FirstDownbeatSeconds),
        _ => throw new ArgumentException("Unknown grid.", nameof(grid)),
    };

    private static string? Cut(string? value) => value is null || value.Length <= MaxTitleLength ? value : value[..MaxTitleLength];
}
