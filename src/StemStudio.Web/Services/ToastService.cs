namespace StemStudio.Web.Services;

/// <summary>Toast. Red stub (T023).</summary>
/// <param name="time">Clock.</param>
public sealed class ToastService(TimeProvider time)
{
    /// <summary>Duration.</summary>
    public static TimeSpan Duration { get; } = TimeSpan.FromSeconds(4);

    /// <summary>Message.</summary>
    public string? Message => time is null ? null : null;
}
