using System.Globalization;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Scores;

namespace StemStudio.Web.Scores;

/// <summary>
/// The tablature line of a ready score item (016 US2, ui-test-ids 1.15.0 <c>asset-score-tab-&lt;id&gt;</c>), for example
/// "Tab: Drop D, capo 2 (3 notes moved by an octave)". Tuning labels are those of data-model 4.1, so the library needs no
/// catalogue call; every limit that is not zero is spelled out.
/// </summary>
public static class TabFactsText
{
    private static readonly Dictionary<TuningIdDto, string> Labels = new()
    {
        [TuningIdDto.GuitarStandard] = "Standard (E A D G B E)",
        [TuningIdDto.GuitarDropD] = "Drop D",
        [TuningIdDto.GuitarHalfStepDown] = "Half step down (Eb)",
        [TuningIdDto.GuitarFullStepDown] = "Full step down (D)",
        [TuningIdDto.GuitarDadgad] = "DADGAD",
        [TuningIdDto.GuitarOpenG] = "Open G",
        [TuningIdDto.GuitarOpenD] = "Open D",
        [TuningIdDto.Bass4Standard] = "4-string (E A D G)",
        [TuningIdDto.Bass4DropD] = "4-string drop D",
        [TuningIdDto.Bass4HalfStepDown] = "4-string half step down",
        [TuningIdDto.Bass5Standard] = "5-string (B E A D G)",
        [TuningIdDto.Bass6Standard] = "6-string (B E A D G C)",
    };

    /// <summary>The label of a catalogue tuning (data-model 4.1).</summary>
    /// <param name="tuning">The tuning.</param>
    /// <returns>For example "Drop D".</returns>
    public static string Label(TuningIdDto tuning) => Labels.TryGetValue(tuning, out var label) ? label : tuning.ToString();

    /// <summary>The tablature line.</summary>
    /// <param name="tab">The tablature facts.</param>
    /// <returns>The text.</returns>
    public static string Of(TablatureFactsDto tab)
    {
        var head = tab.Capo > 0
            ? $"Tab: {Label(tab.Tuning)}, capo {tab.Capo.ToString(CultureInfo.InvariantCulture)}"
            : $"Tab: {Label(tab.Tuning)}";
        string[] limits =
        [
            .. Count(tab.OctaveMoved, "note moved by an octave", "notes moved by an octave"),
            .. Count(tab.NotesDropped, "chord note left out", "chord notes left out"),
            .. Count(tab.WideStretches, "chord with a wide stretch", "chords with a wide stretch"),
        ];
        return limits.Length == 0 ? head : $"{head} ({string.Join(", ", limits)})";
    }

    private static string[] Count(int count, string one, string many) => count switch
    {
        <= 0 => [],
        1 => [$"1 {one}"],
        _ => [$"{count.ToString(CultureInfo.InvariantCulture)} {many}"],
    };
}
