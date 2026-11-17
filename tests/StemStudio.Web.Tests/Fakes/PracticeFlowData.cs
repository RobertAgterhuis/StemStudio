using System.Text.Json;
using StemStudio.Contracts.Practice;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Setlists;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Feature 014 test data built from the contract examples (practice-api.openapi.yaml <c>SectionSetReady</c>, <c>ScoreViewReady</c>, <c>SetlistRehearsal</c>).</summary>
public static class PracticeFlowData
{
    /// <summary>JSON options of the API (web defaults; enums carry their own converters).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Ids of the five example sections.</summary>
    public static readonly Guid[] SectionIds =
    [
        Guid.Parse("0b1c2d3e-0000-4000-8000-000000000001"), Guid.Parse("0b1c2d3e-0000-4000-8000-000000000002"),
        Guid.Parse("0b1c2d3e-0000-4000-8000-000000000003"), Guid.Parse("0b1c2d3e-0000-4000-8000-000000000004"),
        Guid.Parse("0b1c2d3e-0000-4000-8000-000000000005"),
    ];

    /// <summary>The example score asset.</summary>
    public static readonly Guid ScoreAssetId = Guid.Parse("9a8b7c6d-1111-4222-8333-444455556666");

    /// <summary>The example setlist.</summary>
    public static readonly Guid SetlistId = Guid.Parse("5e4d3c2b-aaaa-4bbb-8ccc-dddddddddddd");

    /// <summary>Serializes a value like the API.</summary>
    /// <typeparam name="T">The type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>JSON text.</returns>
    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Json);

    /// <summary>One section.</summary>
    public static SectionDto Section(int index, SectionKindDto kind, string label, double start, double end, string? instrument = null) => new()
    {
        Id = SectionIds[index],
        Kind = kind,
        Label = label,
        StartSeconds = start,
        EndSeconds = end,
        StartBar = (int)(start / 2),
        Instrument = instrument,
        Confidence = 0.8 - (index * 0.05),
        Origin = SectionOriginDto.Detected,
    };

    /// <summary>The five example sections.</summary>
    public static IReadOnlyList<SectionDto> Sections() =>
    [
        Section(0, SectionKindDto.Intro, "Intro", 0, 8.1),
        Section(1, SectionKindDto.Verse, "Verse 1", 8.1, 24.1),
        Section(2, SectionKindDto.Chorus, "Chorus 1", 24.1, 40.1),
        Section(3, SectionKindDto.Solo, "Guitar solo", 40.1, 56.1, "guitar"),
        Section(4, SectionKindDto.Outro, "Outro", 56.1, 64.0),
    ];

    /// <summary>A section set.</summary>
    public static SectionSetDto SectionSet(
        SectionSetStatusDto status = SectionSetStatusDto.Ready, IReadOnlyList<SectionDto>? sections = null, int revision = 1) => new()
    {
        TrackId = TestData.TrackId,
        Status = status,
        Origin = SectionSetOriginDto.Detected,
        Revision = revision,
        Analyzer = "stems-novelty/1.0.0",
        Snap = SectionSnapDto.Downbeat,
        Warnings = [],
        Error = status == SectionSetStatusDto.Failed ? "The analysis could not read the stems." : null,
        Sections = sections ?? (status == SectionSetStatusDto.Ready ? Sections() : []),
        UpdatedAt = new DateTimeOffset(2026, 11, 16, 10, 12, 0, TimeSpan.Zero),
    };

    /// <summary>A score of the track.</summary>
    public static TrackScoreDto TrackScore(Guid? id = null, string name = "Bass part", ScoreViewStatusDto view = ScoreViewStatusDto.Ready, bool manualTempo = false) => new()
    {
        AssetId = id ?? ScoreAssetId,
        Name = name,
        Profile = Contracts.Scores.ScoreProfileDto.Bass,
        Source = "bass",
        ManualTempo = manualTempo,
        CreatedAt = new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero),
        ViewStatus = view,
    };

    /// <summary>A score view.</summary>
    public static ScoreViewDto ScoreView(ScoreViewStatusDto status = ScoreViewStatusDto.Ready, Guid? assetId = null)
    {
        var id = assetId ?? ScoreAssetId;
        var ready = status == ScoreViewStatusDto.Ready;
        return new ScoreViewDto
        {
            AssetId = id,
            Status = status,
            TrackId = TestData.TrackId,
            OffsetSeconds = 0,
            Error = status == ScoreViewStatusDto.Failed ? "The score could not be engraved." : null,
            Pages = ready ? [new ScoreViewPageDto { Page = 1, Url = $"/api/assets/{id}/score-view/pages/1?v=1", WidthPx = 840, HeightPx = 1188 }] : [],
            Bars = ready
                ? [Bar(0, 6.1, 8.1), Bar(1, 8.1, 10.1), Bar(2, 10.1, 12.1)]
                : [],
        };
    }

    private static ScoreBarDto Bar(int index, double start, double end) => new()
    {
        Index = index, Number = index, Page = 1, X = 0.05 + (index * 0.1), Y = 0.1, Width = 0.1, Height = 0.025, StartSeconds = start, EndSeconds = end,
    };

    /// <summary>A setlist entry.</summary>
    public static SetlistEntryDto Entry(int position, Guid? trackId = null, Guid? mixId = null, string note = "") => new()
    {
        Id = Guid.Parse($"6f000000-0000-4000-8000-00000000000{position}"),
        Position = position,
        TrackId = trackId ?? TestData.TrackId,
        TrackTitle = $"Song {position}",
        Artist = "The Band",
        MixId = mixId,
        MixName = mixId is null ? null : "Practice mix",
        Note = note,
        DurationSeconds = 233,
        Bpm = 120,
        SectionCount = 5,
        HasLyrics = true,
    };

    /// <summary>The example setlist (two songs).</summary>
    public static SetlistDto Setlist(IReadOnlyList<SetlistEntryDto>? entries = null, int removed = 0) => new()
    {
        Id = SetlistId,
        Name = "Friday rehearsal",
        Note = "Bring the capo.",
        Entries = entries ?? [Entry(1, note: "Start slow"), Entry(2, Guid.NewGuid(), TestData.MixId)],
        TotalSeconds = 466,
        RemovedEntries = removed,
        CreatedAt = new DateTimeOffset(2026, 11, 14, 18, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 11, 15, 18, 0, 0, TimeSpan.Zero),
    };

    /// <summary>A setlist summary.</summary>
    public static SetlistSummaryDto SetlistSummary(Guid? id = null, string name = "Friday rehearsal") => new()
    {
        Id = id ?? SetlistId, Name = name, Songs = 2, TotalSeconds = 466, UpdatedAt = new DateTimeOffset(2026, 11, 15, 18, 0, 0, TimeSpan.Zero),
    };

    /// <summary>Practice log settings.</summary>
    public static PracticeLogSettingsDto LogSettings(bool enabled = true, bool acknowledged = true) => new()
    {
        Enabled = enabled, NoticeAcknowledged = acknowledged, Sessions = 3, UpdatedAt = null,
    };

    /// <summary>A practice summary.</summary>
    public static PracticeSummaryDto Summary(double today = 720, double week = 2400, double total = 9000) => new()
    {
        TrackId = TestData.TrackId,
        TodaySeconds = today,
        Last7DaysSeconds = week,
        TotalSeconds = total,
        Sessions = 4,
        LastPractisedAt = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero),
        Days = [new PracticeDayDto { Date = new DateOnly(2026, 11, 16), Seconds = 600 }, new PracticeDayDto { Date = new DateOnly(2026, 11, 17), Seconds = 720 }],
        Sections =
        [
            new PracticeSectionTotalDto
            {
                SectionId = SectionIds[3], Kind = "solo", Label = "Guitar solo", Seconds = 900, LoopPasses = 12, BestTrainerPercent = 95,
                LastPractisedAt = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero),
            },
        ],
        Tempo =
        [
            new PracticeTempoPointDto { Date = new DateOnly(2026, 11, 16), SectionId = SectionIds[3], Label = "Guitar solo", Percent = 85 },
            new PracticeTempoPointDto { Date = new DateOnly(2026, 11, 17), SectionId = SectionIds[3], Label = "Guitar solo", Percent = 95 },
        ],
    };

    /// <summary>A stored session.</summary>
    public static PracticeSessionDto Session(Guid? id = null) => new()
    {
        Id = id ?? Guid.Parse("7a000000-0000-4000-8000-000000000001"),
        TrackId = TestData.TrackId,
        MixId = null,
        StartedAt = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero),
        EndedAt = new DateTimeOffset(2026, 11, 17, 9, 12, 0, TimeSpan.Zero),
        PlayedSeconds = 700,
        LoopPasses = 5,
        MaxSpeedPercent = 95,
        Sections = [],
    };

    /// <summary>A track total of the Practice page.</summary>
    public static PracticeTrackTotalDto TrackTotal(Guid? trackId = null, string title = "Fake Song") => new()
    {
        TrackId = trackId ?? TestData.TrackId,
        Title = title,
        Artist = "The Band",
        TotalSeconds = 9000,
        Last7DaysSeconds = 2400,
        Sessions = 4,
        LastPractisedAt = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero),
    };
}
