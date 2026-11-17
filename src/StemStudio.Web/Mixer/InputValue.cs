using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace StemStudio.Web.Mixer;

/// <summary>Parses and formats numeric values of native inputs (invariant culture, as browsers send them).</summary>
public static class InputValue
{
    /// <summary>Parses the value of an <c>input</c>/<c>change</c> event.</summary>
    /// <param name="args">The event.</param>
    /// <returns>The number, or null when empty or not a number.</returns>
    public static double? Parse(ChangeEventArgs args) =>
        double.TryParse(args.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Formats a number for a <c>value</c> or <c>aria-valuenow</c> attribute.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The invariant text, e.g. "12.3".</returns>
    public static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Formats a gain 0..1 as a whole percentage.</summary>
    /// <param name="gain">The gain.</param>
    /// <returns>For example "80".</returns>
    public static string Percent(double gain) => (Math.Round(gain * 100) + 0.0).ToString(CultureInfo.InvariantCulture);

    /// <summary>Parses a whole number within a range (invariant culture, optional sign).</summary>
    /// <param name="text">The entered text.</param>
    /// <param name="min">Smallest accepted value.</param>
    /// <param name="max">Largest accepted value.</param>
    /// <returns>The number, or null when it is not a whole number in range.</returns>
    public static int? Whole(string? text, int min, int max) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max
            ? value
            : null;
}
