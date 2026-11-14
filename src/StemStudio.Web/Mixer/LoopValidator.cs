using StemStudio.Web.Services;

namespace StemStudio.Web.Mixer;

/// <summary>Validates A-B loop markers before they reach the engine (US2-5).</summary>
public static class LoopValidator
{
    /// <summary>Reason when B is not after A.</summary>
    public const string EndBeforeStartMessage = "B must be after A.";

    /// <summary>Reason when looping is enabled before both markers are set.</summary>
    public const string MissingMarkersMessage = "Set both A and B before enabling the loop.";

    /// <summary>Checks <c>0 &lt;= start &lt; end &lt;= duration</c>.</summary>
    /// <param name="start">Marker A in seconds.</param>
    /// <param name="end">Marker B in seconds.</param>
    /// <param name="duration">Track duration in seconds; 0 when unknown (then only the order and A &gt;= 0 are checked).</param>
    /// <returns>A readable reason, or null when the region is valid.</returns>
    public static string? Validate(double start, double end, double duration)
    {
        if (end <= start)
        {
            return EndBeforeStartMessage;
        }

        var outside = start < 0 || (duration > 0 && end > duration);
        return outside ? $"The loop is outside the track (0:00 - {TimeText.Duration(duration)})." : null;
    }
}
