using System.Text.RegularExpressions;
using StemStudio.Contracts;

namespace StemStudio.Agent.Tools;

/// <summary>
/// Values from port results that may appear in trusted (non-<c>untrusted_text</c>) fields: only codes and hardware names
/// that match a strict pattern. Everything else is dropped here; free text belongs in <c>untrusted_text</c>.
/// </summary>
public static partial class TrustedText
{
    /// <summary>A hardware/device name (letters, digits, spaces, parentheses, dot, dash; at most 60 characters), or null.</summary>
    /// <param name="text">Text from a port.</param>
    /// <returns>The name, or null.</returns>
    public static string? SafeName(string? text) => text is not null && NamePattern().IsMatch(text) ? text : null;

    /// <summary>A lower-case code such as a job stage (<c>separating</c>), or null.</summary>
    /// <param name="text">Text from a port.</param>
    /// <returns>The code, or null.</returns>
    public static string? Code(string? text) => text is not null && CodePattern().IsMatch(text) ? text : null;

    /// <summary>True for a valid channel id (standard stem or parent.detail).</summary>
    /// <param name="text">Text from a port.</param>
    /// <returns>True when valid.</returns>
    public static bool IsChannel(string? text) => text is not null && ChannelPattern().IsMatch(text);

    /// <summary>A layer or model name such as <c>Hunspell nl</c> or <c>qwen3.5:9b</c>, or null when it has other characters.</summary>
    /// <param name="text">Server-generated name.</param>
    /// <returns>The name or null.</returns>
    public static string? LayerName(string? text) => text is not null && LayerPattern().IsMatch(text) ? text : null;

    [GeneratedRegex(@"^[A-Za-z0-9 ()._:/-]{1,80}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex LayerPattern();

    [GeneratedRegex(@"^[A-Za-z0-9 ()._-]{1,60}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^[a-z][a-zA-Z-]{0,31}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex CodePattern();

    [GeneratedRegex(ContractLimits.ChannelIdPattern, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ChannelPattern();
}
