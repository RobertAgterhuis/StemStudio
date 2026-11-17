using System.Globalization;

namespace StemStudio.Domain.Recording;

/// <summary>Numbers, default names, contents texts and name rules of takes (data-model 2.1, 2.2).</summary>
public static class TakeNaming
{
    /// <summary>The next number of a track: highest existing number + 1 (1 for the first take).</summary>
    /// <param name="existingNumbers">Numbers of the track's takes.</param>
    /// <returns>The number.</returns>
    public static int NextNumber(IEnumerable<int> existingNumbers)
    {
        ArgumentNullException.ThrowIfNull(existingNumbers);
        return existingNumbers.DefaultIfEmpty(0).Max() + 1;
    }

    /// <summary>The label of a part ("Keys" for piano); null for none.</summary>
    /// <param name="part">The part.</param>
    /// <returns>The label.</returns>
    public static string? PartLabel(RecordingPart part) => part switch
    {
        RecordingPart.None => null,
        RecordingPart.Piano => "Keys",
        _ => part.ToString(),
    };

    /// <summary>"Take n" or "Take n - Part".</summary>
    /// <param name="number">Take number.</param>
    /// <param name="part">Part.</param>
    /// <returns>The name.</returns>
    public static string DefaultName(int number, RecordingPart part)
    {
        var take = string.Create(CultureInfo.InvariantCulture, $"Take {number}");
        return PartLabel(part) is { } label ? $"{take} - {label}" : take;
    }

    /// <summary>The asset contents text: "Take n - Part (recording)".</summary>
    /// <param name="number">Take number.</param>
    /// <param name="part">Part.</param>
    /// <returns>The text.</returns>
    public static string Contents(int number, RecordingPart part) => $"{DefaultName(number, part)} (recording)";

    /// <summary>A valid name: trimmed, 1..80 characters, no control characters.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The trimmed name.</returns>
    /// <exception cref="DomainValidationException">The name breaks a rule.</exception>
    public static string Validate(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        var errors = new ValidationErrors();
        errors.Check(trimmed.Length is >= 1 and <= RecordingConstants.MaxNameLength, "name", $"Must be 1 to {RecordingConstants.MaxNameLength} characters long.");
        errors.Check(!trimmed.Any(char.IsControl), "name", "Must not contain control characters.");
        errors.ThrowIfAny();
        return trimmed;
    }

    /// <summary>A device label as stored: control characters removed, cut to 200 characters; null when empty.</summary>
    /// <param name="label">The label.</param>
    /// <returns>The stored label.</returns>
    public static string? CleanDeviceLabel(string? label)
    {
        var cleaned = new string((label ?? string.Empty).Where(c => !char.IsControl(c)).ToArray());
        return cleaned.Length == 0 ? null : cleaned[..Math.Min(cleaned.Length, RecordingConstants.MaxDeviceLabelLength)];
    }
}
