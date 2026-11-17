using System.Text.Json;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Sections;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.SongSections;

/// <summary>Maps section sets between the domain, the contract (backend 1.13.0) and the worker (1.8.0).</summary>
public static class SectionMapper
{
    /// <summary>The DTO of a set; a missing set is returned with status <c>none</c>.</summary>
    /// <param name="track">The track (for bar numbers).</param>
    /// <param name="set">The stored set, or null.</param>
    /// <returns>The DTO.</returns>
    public static SectionSetDto ToDto(Track track, TrackSections? set)
    {
        ArgumentNullException.ThrowIfNull(track);
        return set is null ? Empty(track.Id) : new SectionSetDto
        {
            TrackId = set.TrackId,
            Status = (SectionSetStatusDto)set.Status,
            Origin = (SectionSetOriginDto)set.Origin,
            Revision = set.Revision,
            Analyzer = set.Analyzer,
            Snap = (SectionSnapDto?)set.Snap,
            Warnings = set.Warnings.Select(ToDto).OfType<SectionWarningDto>().ToList(),
            Error = set.Error,
            Sections = set.Sections.Select(section => ToDto(section, track.BeatGrid)).ToList(),
            UpdatedAt = new DateTimeOffset(set.UpdatedAt, TimeSpan.Zero),
        };
    }

    /// <summary>The domain input of a request section.</summary>
    /// <param name="input">Request section.</param>
    /// <returns>The input.</returns>
    public static SectionInput ToInput(SectionInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new SectionInput(input.Id, (SectionKind)input.Kind, input.Label ?? string.Empty, input.StartSeconds, input.EndSeconds, input.Instrument);
    }

    /// <summary>The domain result of a completed worker job.</summary>
    /// <param name="result">Worker result.</param>
    /// <returns>The detection result.</returns>
    public static DetectionResult ToDetection(WorkerSectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DetectionResult(
            $"{result.Analyzer.Name}/{result.Analyzer.Version}",
            ParseSnap(result.Snap),
            (result.Warnings ?? []).Select(warning => new SectionWarning(warning.Code, warning.Message)).ToList(),
            (result.Sections ?? []).Select(section => new DetectedSection(ParseKind(section.Kind), section.Label, section.StartSeconds, section.EndSeconds, section.Instrument, section.Confidence)).ToList());
    }

    /// <summary>The domain kind of a kebab-case kind (unknown -> other).</summary>
    /// <param name="kind">Wire value.</param>
    /// <returns>The kind.</returns>
    public static SectionKind ParseKind(string? kind) =>
        Parse<SectionKindDto>(kind) is { } dto ? (SectionKind)dto : SectionKind.Other;

    /// <summary>The kebab-case wire value of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>For example <c>pre-chorus</c>.</returns>
    public static string KindName(SectionKind kind) => JsonSerializer.Serialize((SectionKindDto)kind).Trim('"');

    private static SectionSetDto Empty(Guid trackId) => new()
    {
        TrackId = trackId,
        Status = SectionSetStatusDto.None,
        Origin = SectionSetOriginDto.Detected,
        Revision = 0,
        Analyzer = null,
        Snap = null,
        Warnings = [],
        Error = null,
        Sections = [],
        UpdatedAt = null,
    };

    private static SectionDto ToDto(Section section, BeatGrid? grid) => new()
    {
        Id = section.Id,
        Kind = (SectionKindDto)section.Kind,
        Label = section.Label,
        StartSeconds = section.StartSeconds,
        EndSeconds = section.EndSeconds,
        StartBar = SectionSnapper.BarAt(section.StartSeconds, grid),
        Instrument = section.Instrument,
        Confidence = section.Confidence,
        Origin = (SectionOriginDto)section.Origin,
    };

    private static SectionWarningDto? ToDto(SectionWarning warning) =>
        Parse<SectionWarningCodeDto>(warning.Code) is { } code ? new SectionWarningDto { Code = code, Message = warning.Message } : null;

    private static SectionSnap? ParseSnap(string? snap) => Parse<SectionSnapDto>(snap) is { } dto ? (SectionSnap)dto : null;

    private static T? Parse<T>(string? value)
        where T : struct, Enum
    {
        try
        {
            return value is null ? null : JsonSerializer.Deserialize<T>($"\"{value}\"");
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
