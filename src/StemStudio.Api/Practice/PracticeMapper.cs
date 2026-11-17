using System.Text.Json;
using StemStudio.Contracts.Practice;
using StemStudio.Domain.Practice;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Practice;

/// <summary>Maps the practice log between the domain and the backend 1.13.0 contract.</summary>
public static class PracticeMapper
{
    /// <summary>The domain input of a request.</summary>
    /// <param name="input">Request body.</param>
    /// <returns>The input.</returns>
    public static PracticeSessionInput ToInput(PracticeSessionInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new PracticeSessionInput
        {
            TrackId = input.TrackId,
            MixId = input.MixId,
            StartedAt = input.StartedAt.UtcDateTime,
            EndedAt = input.EndedAt.UtcDateTime,
            PlayedSeconds = input.PlayedSeconds,
            LoopPasses = input.LoopPasses,
            MaxSpeedPercent = input.MaxSpeedPercent,
            Sections = (input.Sections ?? []).Select(section => new PracticeSectionInput
            {
                SectionId = section.SectionId,
                Kind = (PracticeSectionKind)section.Kind,
                Label = section.Label ?? string.Empty,
                PlayedSeconds = section.PlayedSeconds,
                LoopPasses = section.LoopPasses,
                TrainerReachedPercent = section.TrainerReachedPercent,
            }).ToList(),
        };
    }

    /// <summary>The DTO of a stored session.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The DTO.</returns>
    public static PracticeSessionDto ToDto(PracticeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new PracticeSessionDto
        {
            Id = session.Id,
            TrackId = session.TrackId,
            MixId = session.MixId,
            StartedAt = Utc(session.StartedAt),
            EndedAt = Utc(session.EndedAt),
            PlayedSeconds = session.PlayedSeconds,
            LoopPasses = session.LoopPasses,
            MaxSpeedPercent = session.MaxSpeedPercent,
            Sections = session.Sections.Select(row => new PracticeSectionInputDto
            {
                SectionId = row.SectionId,
                Kind = (PracticeSectionKindDto)row.Kind,
                Label = row.Label,
                PlayedSeconds = row.PlayedSeconds,
                LoopPasses = row.LoopPasses,
                TrainerReachedPercent = row.TrainerReachedPercent,
            }).ToList(),
        };
    }

    /// <summary>The DTO of the log settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="sessions">Stored sessions.</param>
    /// <returns>The DTO.</returns>
    public static PracticeLogSettingsDto ToDto(PracticeLogSettings settings, int sessions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new PracticeLogSettingsDto
        {
            Enabled = settings.Enabled,
            NoticeAcknowledged = settings.NoticeAcknowledged,
            Sessions = sessions,
            UpdatedAt = settings.UpdatedAt is { } at ? Utc(at) : null,
        };
    }

    /// <summary>The DTO of a summary.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="summary">The computed summary.</param>
    /// <returns>The DTO.</returns>
    public static PracticeSummaryDto ToDto(Guid trackId, PracticeSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new PracticeSummaryDto
        {
            TrackId = trackId,
            TodaySeconds = summary.TodaySeconds,
            Last7DaysSeconds = summary.Last7DaysSeconds,
            TotalSeconds = summary.TotalSeconds,
            Sessions = summary.Sessions,
            LastPractisedAt = summary.LastPractisedAt is { } at ? Utc(at) : null,
            Days = summary.Days.Select(day => new PracticeDayDto { Date = day.Date, Seconds = day.Seconds }).ToList(),
            Sections = summary.Sections.Select(ToDto).ToList(),
            Tempo = summary.Tempo.Select(point => new PracticeTempoPointDto { Date = point.Date, SectionId = point.SectionId, Label = point.Label, Percent = point.Percent }).ToList(),
        };
    }

    /// <summary>The overview row of a track.</summary>
    /// <param name="total">The totals.</param>
    /// <param name="track">The track.</param>
    /// <returns>The DTO.</returns>
    public static PracticeTrackTotalDto ToDto(PracticeTrackTotal total, Track track)
    {
        ArgumentNullException.ThrowIfNull(total);
        ArgumentNullException.ThrowIfNull(track);
        return new PracticeTrackTotalDto
        {
            TrackId = total.TrackId,
            Title = track.Title,
            Artist = track.Artist,
            TotalSeconds = total.TotalSeconds,
            Last7DaysSeconds = total.Last7DaysSeconds,
            Sessions = total.Sessions,
            LastPractisedAt = Utc(total.LastPractisedAt),
        };
    }

    private static PracticeSectionTotalDto ToDto(PracticeSectionTotal total) => new()
    {
        SectionId = total.SectionId,
        Kind = JsonSerializer.Serialize((PracticeSectionKindDto)total.Kind).Trim('"'),
        Label = total.Label,
        Seconds = total.Seconds,
        LoopPasses = total.LoopPasses,
        BestTrainerPercent = total.BestTrainerPercent,
        LastPractisedAt = Utc(total.LastPractisedAt),
    };

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);
}
