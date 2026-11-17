namespace StemStudio.Web.Practice;

/// <summary>State of the <see cref="PracticeRecorder"/> (014 data-model 5.4).</summary>
public enum PracticeRecorderState
{
    /// <summary>A track is open; nothing played yet.</summary>
    Idle = 0,

    /// <summary>Playing: time is added to the session.</summary>
    Running = 1,

    /// <summary>Paused or stopped; playing again within 10 minutes continues the session.</summary>
    Holding = 2,

    /// <summary>The session ended (page leave, track change); playing again starts a new one.</summary>
    Closed = 3,

    /// <summary>The log is off (409 <c>practice-log-off</c>): nothing is recorded until the next track context.</summary>
    Off = 4,
}
