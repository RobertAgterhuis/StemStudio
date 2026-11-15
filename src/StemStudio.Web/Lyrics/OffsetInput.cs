using System.Globalization;
using StemStudio.Contracts;

namespace StemStudio.Web.Lyrics;

/// <summary>
/// The karaoke offset slider's values (FR-007): -5000..5000 ms in steps of 10; the arrow keys move it by
/// <see cref="KeyStepMs"/>. A native range input moves one step (10 ms) per arrow key, so an input that follows an
/// arrow key is widened to the key step.
/// </summary>
public static class OffsetInput
{
    /// <summary>The offset change of one arrow key press.</summary>
    public const int KeyStepMs = 50;

    /// <summary>Whether a key is one of the four arrow keys.</summary>
    /// <param name="key">The <c>KeyboardEvent.key</c> value.</param>
    /// <returns>True for an arrow key.</returns>
    public static bool IsArrow(string key) => key is "ArrowRight" or "ArrowUp" or "ArrowLeft" or "ArrowDown";

    /// <summary>Parses a typed or slid value, snapped to 10 ms and clamped to the contract range.</summary>
    /// <param name="value">The input value.</param>
    /// <returns>The offset, or null when the value is not a number.</returns>
    public static int? Parse(object? value) =>
        double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms)
            ? Clamp((int)Math.Round(ms / ContractLimits.LyricsOffsetStepMs, MidpointRounding.AwayFromZero) * ContractLimits.LyricsOffsetStepMs)
            : null;

    /// <summary>The offset after an arrow key: one native step in the same direction becomes <see cref="KeyStepMs"/>.</summary>
    /// <param name="previous">The offset before the key.</param>
    /// <param name="next">The value the native input reported.</param>
    /// <returns>The widened, clamped offset.</returns>
    public static int AfterKey(int previous, int next) => Clamp(previous + (Math.Sign(next - previous) * KeyStepMs));

    /// <summary>Clamps to -5000..5000 ms.</summary>
    /// <param name="ms">An offset.</param>
    /// <returns>The clamped offset.</returns>
    public static int Clamp(int ms) => Math.Clamp(ms, -ContractLimits.MaxLyricsOffsetMs, ContractLimits.MaxLyricsOffsetMs);

    /// <summary>The <c>aria-valuetext</c>: "-250 ms".</summary>
    /// <param name="ms">The offset.</param>
    /// <returns>The text.</returns>
    public static string Text(int ms) => $"{ms.ToString(CultureInfo.InvariantCulture)} ms";
}
