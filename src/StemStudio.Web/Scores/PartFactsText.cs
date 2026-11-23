using System.Globalization;
using StemStudio.Contracts.Scores;

namespace StemStudio.Web.Scores;

/// <summary>
/// The part line of a ready score item (016 US5, ui-test-ids 1.15.0 <c>asset-score-part-&lt;id&gt;</c>), for example
/// "Alto Saxophone in E-flat, treble clef (2 notes out of range)".
/// </summary>
public static class PartFactsText
{
    /// <summary>The part line.</summary>
    /// <param name="part">The part facts.</param>
    /// <returns>The text.</returns>
    public static string Of(PartFactsDto part)
    {
        var head = $"{part.Name}, {ClefLabel(part.Clef).ToLowerInvariant()} clef";
        return part.OutOfRange switch
        {
            <= 0 => head,
            1 => $"{head} (1 note out of range)",
            _ => string.Create(CultureInfo.InvariantCulture, $"{head} ({part.OutOfRange} notes out of range)"),
        };
    }

    /// <summary>The label of a clef wire name: "Treble 8vb" for <c>treble-8vb</c>, "Automatic" for <c>auto</c>.</summary>
    /// <param name="clef">The wire name.</param>
    /// <returns>The label.</returns>
    public static string ClefLabel(string clef) => clef switch
    {
        "auto" => "Automatic",
        _ => char.ToUpperInvariant(clef[0]) + clef[1..].Replace('-', ' '),
    };
}
