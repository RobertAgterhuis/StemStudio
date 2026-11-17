using StemStudio.Contracts.Practice;
using StemStudio.Contracts.Sections;

namespace StemStudio.Web.Practice;

/// <summary>Cumulative values of one practice session (014 data-model 3.7), turned into the upsert body on every flush.</summary>
/// <param name="id">Session id (made by the browser).</param>
/// <param name="startedAt">Start time.</param>
internal sealed class PracticeTally(Guid id, DateTimeOffset startedAt)
{
    /// <summary>Lowest stored speed percent.</summary>
    public const double MinSpeed = 50;

    /// <summary>Highest stored speed percent.</summary>
    public const double MaxSpeed = 120;

    private readonly Dictionary<Guid, SectionTally> _sections = [];
    private double _maxSpeed;
    private int _passes;

    /// <summary>Session id.</summary>
    public Guid Id { get; } = id;

    /// <summary>Playing seconds so far.</summary>
    public double PlayedSeconds { get; private set; }

    /// <summary>Adds playing time, to the section when there is one.</summary>
    /// <param name="seconds">Elapsed playing seconds.</param>
    /// <param name="section">The section of the last position, or null.</param>
    public void AddTime(double seconds, SectionDto? section)
    {
        PlayedSeconds += seconds;
        if (section is not null)
        {
            Row(section).Seconds += seconds;
        }
    }

    /// <summary>Records the playback speed heard.</summary>
    /// <param name="percent">Speed in percent.</param>
    public void Speed(double percent) => _maxSpeed = Math.Max(_maxSpeed, percent);

    /// <summary>Counts a loop pass, for the section when there is one; the trainer percent when the pass was a trainer pass over the whole section.</summary>
    /// <param name="section">The looped section, or null.</param>
    /// <param name="trainerPercent">Speed of a full-section trainer pass, or null.</param>
    public void AddPass(SectionDto? section, double? trainerPercent)
    {
        _passes++;
        if (section is null)
        {
            return;
        }

        var row = Row(section);
        row.Passes++;
        if (trainerPercent is { } percent)
        {
            row.Trainer = Math.Max(row.Trainer ?? 0, Clamp(percent));
        }
    }

    /// <summary>The upsert body (cumulative values).</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="mixId">Mix, or null.</param>
    /// <param name="endedAt">Flush time.</param>
    /// <returns>The body.</returns>
    public PracticeSessionInputDto ToInput(Guid trackId, Guid? mixId, DateTimeOffset endedAt) => new()
    {
        TrackId = trackId,
        MixId = mixId,
        StartedAt = startedAt,
        EndedAt = endedAt,
        PlayedSeconds = Math.Round(PlayedSeconds, 3),
        LoopPasses = _passes,
        MaxSpeedPercent = _maxSpeed > 0 ? Clamp(_maxSpeed) : null,
        Sections = _sections.Count == 0 ? null : [.. _sections.Values.Take(64).Select(r => r.ToInput())],
    };

    private static double Clamp(double percent) => Math.Clamp(Math.Round(percent, 1), MinSpeed, MaxSpeed);

    private SectionTally Row(SectionDto section)
    {
        if (!_sections.TryGetValue(section.Id, out var row))
        {
            row = new SectionTally(section);
            _sections[section.Id] = row;
        }

        return row;
    }

    private sealed class SectionTally(SectionDto section)
    {
        public double Seconds { get; set; }

        public int Passes { get; set; }

        public double? Trainer { get; set; }

        public PracticeSectionInputDto ToInput() => new()
        {
            SectionId = section.Id,
            Kind = Enum.Parse<PracticeSectionKindDto>(section.Kind.ToString()),
            Label = section.Label,
            PlayedSeconds = Math.Round(Seconds, 3),
            LoopPasses = Passes,
            TrainerReachedPercent = Trainer,
        };
    }
}
