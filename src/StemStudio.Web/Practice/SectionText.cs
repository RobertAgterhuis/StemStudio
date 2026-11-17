using System.Globalization;
using StemStudio.Contracts.Sections;
using StemStudio.Web.Services;

namespace StemStudio.Web.Practice;

/// <summary>Display texts of sections (014 ui-test-ids 1.13.0, FR-007).</summary>
public static class SectionText
{
    /// <summary>Status line while detecting.</summary>
    public const string Detecting = "Detecting sections...";

    /// <summary>Status line of detected (not edited) sections.</summary>
    public const string Detected = "Detected automatically - check and edit";

    /// <summary>Status line when the beat grid is unreliable or missing (FR-003).</summary>
    public const string GridUncertain = "Tempo grid uncertain - boundaries may be off";

    /// <summary>Failure line when the server gave no reason.</summary>
    public const string Failed = "Sections could not be detected.";

    /// <summary>The kind as shown to the user.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>For example "pre-chorus".</returns>
    public static string Kind(SectionKindDto kind) => kind == SectionKindDto.PreChorus ? "pre-chorus" : kind.ToString().ToLowerInvariant();

    /// <summary>The <c>data-kind</c> value (the contract wire name).</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>For example <c>pre-chorus</c>.</returns>
    public static string WireKind(SectionKindDto kind) => WireName.Of(kind);

    /// <summary>Seconds with one decimal (<c>data-start</c>, <c>data-end</c>).</summary>
    /// <param name="seconds">Seconds.</param>
    /// <returns>For example "40.1".</returns>
    public static string Seconds(double seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>The range of a section: bars when known (1-based, end = next section's start bar), else times.</summary>
    /// <param name="sections">All sections in order.</param>
    /// <param name="section">The section.</param>
    /// <returns>For example "bars 20 to 27" or "0:40 to 0:56".</returns>
    public static string Range(IReadOnlyList<SectionDto> sections, SectionDto section)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(section);
        var next = sections.SkipWhile(s => s.Id != section.Id).Skip(1).FirstOrDefault();
        return section.StartBar is { } start && next?.StartBar is { } end && end > start
            ? string.Create(CultureInfo.InvariantCulture, $"bars {start + 1} to {end}")
            : $"{TimeText.Duration(section.StartSeconds)} to {TimeText.Duration(section.EndSeconds)}";
    }

    /// <summary>The status line of a section set (null when there is nothing to say).</summary>
    /// <param name="set">The set, or null while loading.</param>
    /// <returns>The status text.</returns>
    public static string? Status(SectionSetDto? set) => set?.Status switch
    {
        null or SectionSetStatusDto.None or SectionSetStatusDto.Queued or SectionSetStatusDto.Detecting => Detecting,
        SectionSetStatusDto.Failed => string.IsNullOrWhiteSpace(set.Error) ? Failed : set.Error,
        _ when set.Warnings.Any(w => w.Code is SectionWarningCodeDto.GridUnreliable or SectionWarningCodeDto.NoGrid) => GridUncertain,
        _ when set.Origin == SectionSetOriginDto.Detected => Detected,
        _ => null,
    };
}
