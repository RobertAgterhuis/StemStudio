using System.Globalization;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Services;

namespace StemStudio.Web.Export;

/// <summary>Start and end of an export (feature 007, FR-006) or score (feature 008, FR-013) range as typed, or the loop region.</summary>
/// <param name="loop">The mixer's loop region, if any.</param>
/// <param name="duration">Length of the song in seconds.</param>
/// <param name="minSeconds">Shortest allowed range: 0.5 s for exports, 2 s for sheet music.</param>
public sealed class RangeInput(LoopRegionDto? loop, double duration, double minSeconds = ContractLimits.MinExportRangeSeconds)
{
    /// <summary>Message when only one of start and end is given or a value is not a number.</summary>
    public const string IncompleteMessage = "Enter start and end in seconds, or leave both empty for the whole song.";

    /// <summary>Message for an export range shorter than <see cref="ContractLimits.MinExportRangeSeconds"/>.</summary>
    public const string TooShortMessage = "The range must be at least 0.5 s long and end after it starts.";

    /// <summary>The loop region, when the mixer has one.</summary>
    public LoopRegionDto? Loop { get; } = loop;

    /// <summary>True while "Only the loop region" is ticked.</summary>
    public bool LoopOnly { get; private set; }

    /// <summary>Start in seconds as typed.</summary>
    public string Start { get; set; } = string.Empty;

    /// <summary>End in seconds as typed.</summary>
    public string End { get; set; } = string.Empty;

    /// <summary>Ticks or unticks "Only the loop region": fills start and end from the loop, or clears them.</summary>
    /// <param name="on">The new state.</param>
    public void SetLoopOnly(bool on)
    {
        LoopOnly = on && Loop is not null;
        (Start, End) = LoopOnly ? (Text(Loop!.Start), Text(Loop.End)) : (string.Empty, string.Empty);
    }

    /// <summary>Checks the range.</summary>
    /// <returns>A readable problem, or null (also for "whole song").</returns>
    public string? Validate()
    {
        if (IsWholeSong)
        {
            return null;
        }

        return TryParse(Start, out var start) && TryParse(End, out var end) ? Check(start, end) : IncompleteMessage;
    }

    /// <summary>The range to send, or null for the whole song (call <see cref="Validate"/> first).</summary>
    /// <returns>The range.</returns>
    public RangeDto? ToDto() =>
        IsWholeSong ? null : new RangeDto(double.Parse(Start, CultureInfo.InvariantCulture), double.Parse(End, CultureInfo.InvariantCulture));

    private string? Check(double start, double end) =>
        end - start < minSeconds ? ShortMessage
        : start < 0 || end > duration ? $"The range must lie inside the song (0 to {TimeText.Duration(duration)})."
        : null;

    private string ShortMessage => $"The range must be at least {minSeconds.ToString(CultureInfo.InvariantCulture)} s long and end after it starts.";

    private bool IsWholeSong => Start.Trim().Length == 0 && End.Trim().Length == 0;

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string Text(double seconds) => Math.Round(seconds, 3).ToString(CultureInfo.InvariantCulture);
}
