namespace StemStudio.Domain.Practice;

/// <summary>One practice session of a track (data-model 3.7); the browser makes the id and sends cumulative values.</summary>
public sealed class PracticeSession
{
    private List<PracticeSectionTime> _sections = [];

    private PracticeSession()
    {
    }

    /// <summary>Session id (made by the browser).</summary>
    public Guid Id { get; private set; }

    /// <summary>The track.</summary>
    public Guid TrackId { get; private set; }

    /// <summary>The mix in use (null after the mix is deleted).</summary>
    public Guid? MixId { get; private set; }

    /// <summary>Start (UTC).</summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>End (UTC).</summary>
    public DateTime EndedAt { get; private set; }

    /// <summary>Played seconds.</summary>
    public double PlayedSeconds { get; private set; }

    /// <summary>Completed loop passes.</summary>
    public int LoopPasses { get; private set; }

    /// <summary>Highest playback speed, or null.</summary>
    public double? MaxSpeedPercent { get; private set; }

    /// <summary>Time per section.</summary>
    public IReadOnlyList<PracticeSectionTime> Sections => _sections;

    /// <summary>Creates a session.</summary>
    /// <param name="id">Id from the browser.</param>
    /// <param name="input">The values.</param>
    /// <param name="nowUtc">Server time (future-start rule).</param>
    /// <returns>The session.</returns>
    /// <exception cref="DomainValidationException">A rule is broken.</exception>
    public static PracticeSession Create(Guid id, PracticeSessionInput input, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(input);
        var session = new PracticeSession { Id = id, TrackId = input.TrackId };
        session.Replace(input, nowUtc);
        return session;
    }

    /// <summary>Replaces the values and the section rows (cumulative upsert).</summary>
    /// <param name="input">The values.</param>
    /// <param name="nowUtc">Server time.</param>
    /// <exception cref="DomainValidationException">A rule is broken or the track differs.</exception>
    public void Replace(PracticeSessionInput input, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = PracticeSessionRules.Validate(input, nowUtc).ToList();
        errors.AddRange(input.TrackId == TrackId ? [] : [new DomainError("trackId", "A session cannot move to another track.")]);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        (MixId, StartedAt, EndedAt) = (input.MixId, input.StartedAt, input.EndedAt);
        (PlayedSeconds, LoopPasses, MaxSpeedPercent) = (input.PlayedSeconds, input.LoopPasses, input.MaxSpeedPercent);
        _sections = (input.Sections ?? []).Select((section, index) => PracticeSectionTime.Create(Id, index, section)).ToList();
    }
}

/// <summary>Practice time of one section within a session (owned by the session, replaced on every upsert).</summary>
public sealed class PracticeSectionTime
{
    private PracticeSectionTime()
    {
        Label = string.Empty;
    }

    /// <summary>The session.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>Row number within the session.</summary>
    public int Index { get; private set; }

    /// <summary>Section id at recording time (no foreign key: survives re-detection), or null.</summary>
    public Guid? SectionId { get; private set; }

    /// <summary>Kind snapshot.</summary>
    public PracticeSectionKind Kind { get; private set; }

    /// <summary>Label snapshot.</summary>
    public string Label { get; private set; }

    /// <summary>Played seconds.</summary>
    public double PlayedSeconds { get; private set; }

    /// <summary>Completed loop passes.</summary>
    public int LoopPasses { get; private set; }

    /// <summary>Highest speed of a completed trainer pass, or null.</summary>
    public double? TrainerReachedPercent { get; private set; }

    internal static PracticeSectionTime Create(Guid sessionId, int index, PracticeSectionInput input) => new()
    {
        SessionId = sessionId,
        Index = index,
        SectionId = input.SectionId,
        Kind = input.Kind,
        Label = PracticeSessionRules.Clean(input.Label),
        PlayedSeconds = input.PlayedSeconds,
        LoopPasses = input.LoopPasses,
        TrainerReachedPercent = input.TrainerReachedPercent,
    };
}
