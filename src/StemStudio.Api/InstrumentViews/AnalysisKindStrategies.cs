using StemStudio.Api.Worker;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.InstrumentViews;

/// <summary>A prerequisite of an analysis that is missing.</summary>
public enum MissingPrerequisite
{
    /// <summary>Nothing is missing.</summary>
    None,

    /// <summary>The standard separation has not completed (<c>409 track-not-ready</c>).</summary>
    TrackNotReady,

    /// <summary>The drums are not split (<c>409 drums-not-split</c>, status unavailable).</summary>
    DrumsNotSplit,

    /// <summary>No vocal stem (<c>409 no-vocals</c>, status unavailable).</summary>
    NoVocals,

    /// <summary>No harmonic stem (status unavailable).</summary>
    NoHarmonicStem,
}

/// <summary>What a run sends and reads: the worker request and the input channels for the fingerprint.</summary>
/// <param name="Request">The worker request.</param>
/// <param name="Inputs">The input channels (fingerprint).</param>
/// <param name="Engine">The engine expected to run (for the estimate).</param>
public sealed record AnalysisPlan(WorkerAnalysisRequest Request, IReadOnlyList<RenderChannel> Inputs, string Engine);

/// <summary>The plan of a run, or the missing prerequisite.</summary>
/// <param name="Missing">The missing prerequisite (<see cref="MissingPrerequisite.None"/> with a plan).</param>
/// <param name="Plan">The plan, or null.</param>
public sealed record PlanResult(MissingPrerequisite Missing, AnalysisPlan? Plan)
{
    /// <summary>A refusal.</summary>
    /// <param name="missing">What is missing.</param>
    /// <returns>The result.</returns>
    public static PlanResult Refused(MissingPrerequisite missing) => new(missing, null);
}

/// <summary>The engines the worker reports (feature 016 health); defaults when the health is unknown.</summary>
/// <param name="ChordEngine">Configured chord engine.</param>
/// <param name="PitchEngine">Configured pitch engine.</param>
public sealed record WorkerEngines(string ChordEngine, string PitchEngine)
{
    /// <summary>The worker defaults (<c>btc</c>, <c>pyin</c>).</summary>
    public static WorkerEngines Defaults { get; } = new("btc", "pyin");
}

/// <summary>
/// The per-kind input rules of data-model 7.4: chords read the cached standard harmonic stems (<c>bass</c>, <c>guitar</c>,
/// <c>piano</c>, <c>other</c>) with the reliable grid; drum hits the five drum detail stems; the reference melody the kept
/// lead stem of a vocals split, else the vocals stem.
/// </summary>
public static class AnalysisKindStrategies
{
    /// <summary>Reason of an unavailable drum hits analysis.</summary>
    public const string SplitDrumsFirst = "Split the drums first.";

    /// <summary>Reason of an unavailable reference melody.</summary>
    public const string NoVocalStem = "This track has no vocal stem.";

    /// <summary>Reason of an unavailable chord chart.</summary>
    public const string NoHarmonicStemReason = "This track has no harmonic stem (bass, guitar, piano or other).";

    private static readonly string[] HarmonicStems = ["bass", "guitar", "piano", "other"];
    private static readonly string[] DrumPieces = ["kick", "snare", "toms", "hihat", "cymbals"];

    /// <summary>The plan of a run.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="track">The track.</param>
    /// <param name="options">The run options.</param>
    /// <param name="engines">The worker's engines.</param>
    /// <returns>The plan or the missing prerequisite.</returns>
    public static PlanResult Plan(AnalysisKind kind, Track track, AnalysisOptions options, WorkerEngines engines)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(engines);
        return kind switch
        {
            AnalysisKind.Chords => Chords(track, options, engines),
            AnalysisKind.DrumHits => DrumHits(track),
            _ => VocalPitch(track, engines),
        };
    }

    /// <summary>The prerequisite that is missing right now (no request built).</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="track">The track.</param>
    /// <returns>The missing prerequisite.</returns>
    public static MissingPrerequisite Missing(AnalysisKind kind, Track track) => Plan(kind, track, AnalysisOptions.Empty, WorkerEngines.Defaults).Missing;

    /// <summary>The unavailable reason of a missing prerequisite, or null when it is not an unavailable state.</summary>
    /// <param name="missing">The missing prerequisite.</param>
    /// <returns>The reason, or null.</returns>
    public static string? UnavailableReason(MissingPrerequisite missing) => missing switch
    {
        MissingPrerequisite.DrumsNotSplit => SplitDrumsFirst,
        MissingPrerequisite.NoVocals => NoVocalStem,
        MissingPrerequisite.NoHarmonicStem => NoHarmonicStemReason,
        _ => null,
    };

    /// <summary>The drum pieces the drums split kept, or empty when the drums are not split (files present).</summary>
    /// <param name="track">The track.</param>
    /// <returns>Piece names in catalogue order.</returns>
    public static IReadOnlyList<string> SplitDrumPieces(Track track) => KeptDetails(track, "drums").Where(DrumPieces.Contains).ToList();

    private static PlanResult Chords(Track track, AnalysisOptions options, WorkerEngines engines)
    {
        if (track.Status != TrackStatus.Completed)
        {
            return PlanResult.Refused(MissingPrerequisite.TrackNotReady);
        }

        var inputs = HarmonicStems.Where(stem => track.Stems.Any(kept => kept.Name == stem)).Select(stem => new RenderChannel(stem, null)).ToList();
        if (inputs.Count == 0)
        {
            return PlanResult.Refused(MissingPrerequisite.NoHarmonicStem);
        }

        var request = new WorkerChordsRequest
        {
            Inputs = inputs.Select(input => input.PathFor(track.Id)).ToList(),
            BassInput = inputs.FirstOrDefault(input => input.Parent == "bass")?.PathFor(track.Id),
            Grid = track.BeatGrid is { Reliable: true } grid ? new WorkerBeatGridTempo(grid.Bpm, grid.BeatsPerBar, grid.Beats, grid.Downbeats) : null,
            Vocabulary = options.Vocabulary == ChordVocabulary.Full ? "full" : "simple",
            Engine = options.Engine,
        };
        return new PlanResult(MissingPrerequisite.None, new AnalysisPlan(request, inputs, options.Engine ?? engines.ChordEngine));
    }

    private static PlanResult DrumHits(Track track)
    {
        var pieces = SplitDrumPieces(track);
        if (track.Status != TrackStatus.Completed || pieces.Count == 0)
        {
            return PlanResult.Refused(MissingPrerequisite.DrumsNotSplit);
        }

        var inputs = pieces.Select(piece => new RenderChannel("drums", piece)).ToList();
        string? PathOf(string piece) => pieces.Contains(piece) ? new RenderChannel("drums", piece).PathFor(track.Id) : null;
        var request = new WorkerDrumHitsRequest { DrumInputs = new WorkerDrumInputs(PathOf("kick"), PathOf("snare"), PathOf("toms"), PathOf("hihat"), PathOf("cymbals")) };
        return new PlanResult(MissingPrerequisite.None, new AnalysisPlan(request, inputs, "drum-onsets"));
    }

    private static PlanResult VocalPitch(Track track, WorkerEngines engines)
    {
        if (track.Status != TrackStatus.Completed)
        {
            return PlanResult.Refused(MissingPrerequisite.TrackNotReady);
        }

        var lead = KeptDetails(track, "vocals").Contains("lead");
        if (!lead && track.Stems.All(stem => stem.Name != "vocals"))
        {
            return PlanResult.Refused(MissingPrerequisite.NoVocals);
        }

        var input = lead ? new RenderChannel("vocals", "lead") : new RenderChannel("vocals", null);
        var request = new WorkerVocalPitchRequest { Input = input.PathFor(track.Id), Source = lead ? "vocals.lead" : "vocals" };
        return new PlanResult(MissingPrerequisite.None, new AnalysisPlan(request, [input], engines.PitchEngine));
    }

    private static IEnumerable<string> KeptDetails(Track track, string parent) =>
        track.HasDetailFiles
            ? track.DetailPhase!.Parents.Where(outcome => outcome.Parent == parent && outcome.Outcome == ParentOutcomeKind.Split).SelectMany(outcome => outcome.Stems).Select(stem => stem.Name)
            : [];
}
