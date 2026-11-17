namespace StemStudio.Web.Recording;

/// <summary>
/// Service registration of the play-along recording UI (feature 015). Stub added by the 014 frontend lane (hook request
/// L6 2) so <c>Program.cs</c> builds before the 015 lane's registrations arrive; owned and replaced by the 015 frontend lane.
/// </summary>
public static class RecordingUiRegistration
{
    /// <summary>Registers the recording UI services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddRecordingUi(this IServiceCollection services) => services;
}
