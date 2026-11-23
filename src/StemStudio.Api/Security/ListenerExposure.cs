using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace StemStudio.Api.Security;

/// <summary>
/// Start-up warning of refactor 003 step 5: outside a container the API is meant for this computer only, so a listener on a
/// non-loopback address (<c>0.0.0.0</c>, <c>[::]</c>, <c>*</c>, a LAN address) is logged once as <c>ApiListensBeyondLoopback</c>
/// (4106). In a container the listener is always <c>[::]</c> and the compose port binding decides (refactor 004), so nothing is
/// logged there.
/// </summary>
public static partial class ListenerExposure
{
    /// <summary>The variable the .NET container images set to <c>true</c>.</summary>
    public const string ContainerVariable = "DOTNET_RUNNING_IN_CONTAINER";

    /// <summary>Registers the warning for the moment the server has bound its addresses.</summary>
    /// <param name="app">The application.</param>
    public static void WarnWhenStarted(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Lifetime.ApplicationStarted.Register(() => LogStartupWarning(
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ListenerExposure).FullName!),
            app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses ?? [],
            IsContainer(Environment.GetEnvironmentVariable(ContainerVariable))));
    }

    /// <summary>True when the value of <see cref="ContainerVariable"/> says the process runs in a container.</summary>
    /// <param name="value">The variable's value.</param>
    /// <returns>True in a container.</returns>
    public static bool IsContainer(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the listener address is a loopback name or address; anything unparsable counts as exposed.</summary>
    /// <param name="address">A server address such as <c>http://localhost:8081</c>.</param>
    /// <returns>True for loopback.</returns>
    public static bool IsLoopback(string address) => Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.IsLoopback;

    /// <summary>Logs one warning naming the non-loopback addresses, unless in a container or there are none.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="addresses">The server's listener addresses.</param>
    /// <param name="inContainer">True in a container.</param>
    public static void LogStartupWarning(ILogger logger, IEnumerable<string> addresses, bool inContainer)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(addresses);
        var exposed = addresses.Where(address => !IsLoopback(address)).ToArray();
        if (!inContainer && exposed.Length > 0)
        {
            LogBeyondLoopback(logger, string.Join(", ", exposed));
        }
    }

    [LoggerMessage(EventId = 4106, EventName = "ApiListensBeyondLoopback", Level = LogLevel.Warning,
        Message = "The StemStudio API listens on {Addresses}: other devices on the network can reach it. It has no login; listen on localhost only unless you trust that network")]
    private static partial void LogBeyondLoopback(ILogger logger, string addresses);
}
