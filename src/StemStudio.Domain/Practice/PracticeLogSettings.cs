namespace StemStudio.Domain.Practice;

/// <summary>The practice log switch (single row, data-model 3.7; not 012's mix <c>PracticeSettings</c>).</summary>
public sealed class PracticeLogSettings
{
    /// <summary>The id of the only row.</summary>
    public const int SingletonId = 1;

    private PracticeLogSettings()
    {
    }

    /// <summary>Always <see cref="SingletonId"/>.</summary>
    public int Id { get; private set; }

    /// <summary>Whether sessions are recorded (default on).</summary>
    public bool Enabled { get; private set; }

    /// <summary>Whether the first-use notice was acknowledged.</summary>
    public bool NoticeAcknowledged { get; private set; }

    /// <summary>Last change (UTC), or null when never saved.</summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>The defaults: on, notice not acknowledged.</summary>
    /// <returns>The settings.</returns>
    public static PracticeLogSettings CreateDefault() => new() { Id = SingletonId, Enabled = true };

    /// <summary>Changes the given fields; null leaves a field unchanged.</summary>
    /// <param name="enabled">New switch value, or null.</param>
    /// <param name="noticeAcknowledged">New notice value, or null.</param>
    /// <param name="nowUtc">Now.</param>
    public void Update(bool? enabled, bool? noticeAcknowledged, DateTime nowUtc) =>
        (Enabled, NoticeAcknowledged, UpdatedAt) = (enabled ?? Enabled, noticeAcknowledged ?? NoticeAcknowledged, nowUtc);
}
