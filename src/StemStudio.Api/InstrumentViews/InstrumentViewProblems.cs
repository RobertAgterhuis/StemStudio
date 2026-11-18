using StemStudio.Api.SongSections;
using StemStudio.Contracts;

namespace StemStudio.Api.InstrumentViews;

/// <summary>Problem responses of feature 016 (type <c>https://stemstudio.local/problems/&lt;code&gt;</c>, <see cref="InstrumentViewProblemTypes"/>).</summary>
public static class InstrumentViewProblems
{
    /// <summary>The 409/503 problem of a refused start, or null for a stored one.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="kindText">What the analysis finds ("chords", "drum hits", "the reference melody").</param>
    /// <returns>The problem, or null.</returns>
    public static IResult? For(StartOutcome outcome, string kindText) => outcome switch
    {
        StartOutcome.TrackNotReady => PracticeFlowProblems.Conflict(InstrumentViewProblemTypes.TrackNotReady, "The track is not ready", $"The {kindText} can be found when the separation has completed."),
        StartOutcome.DrumsNotSplit => PracticeFlowProblems.Conflict(InstrumentViewProblemTypes.DrumsNotSplit, "The drums are not split", "Split the drums first."),
        StartOutcome.NoVocals => PracticeFlowProblems.Conflict(InstrumentViewProblemTypes.NoVocals, "No vocals", "This track has no vocal stem."),
        StartOutcome.AnalysisRunning => PracticeFlowProblems.Conflict(InstrumentViewProblemTypes.AnalysisRunning, "The analysis is running", $"Wait until the {kindText} have been found."),
        StartOutcome.EditsExist => PracticeFlowProblems.Conflict(InstrumentViewProblemTypes.EditsExist, "The chords were edited", "Finding the chords again replaces your corrections; confirm to overwrite them."),
        StartOutcome.WorkerUnavailable => PracticeFlowProblems.WorkerUnavailable(),
        StartOutcome.TrackNotFound => ApiProblems.NotFound("The track does not exist."),
        _ => null,
    };

    /// <summary>409 <c>chords-not-ready</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult ChordsNotReady() =>
        PracticeFlowProblems.Conflict(InstrumentViewProblemTypes.ChordsNotReady, "The chords are not ready", "Find the chords of this track first.");

    /// <summary>422 when the display transpose or capo is out of range.</summary>
    /// <param name="transpose">The transpose.</param>
    /// <param name="capo">The capo, or null when the operation has none.</param>
    /// <returns>The problem, or null when both are in range.</returns>
    public static IResult? DisplayOutOfRange(int transpose, int? capo)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (transpose is < ContractLimits.MinDisplayTranspose or > ContractLimits.MaxDisplayTranspose)
        {
            errors["transpose"] = [$"Must be between {ContractLimits.MinDisplayTranspose} and {ContractLimits.MaxDisplayTranspose}."];
        }

        if (capo is < 0 or > ContractLimits.CapoMax)
        {
            errors["capo"] = [$"Must be between 0 and {ContractLimits.CapoMax}."];
        }

        return errors.Count == 0 ? null : ApiProblems.Unprocessable(errors);
    }
}
