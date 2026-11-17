using Microsoft.AspNetCore.Components;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>
/// A <see cref="NavigationManager"/> that behaves like Blazor Server: <see cref="NavigationManager.NavigateTo(string, NavigationOptions)"/>
/// only records the request; the location change ("echo") comes back later, when the test releases it with
/// <see cref="Echo"/> - in any order, twice, or never. <see cref="Go"/> is a genuine navigation (Back, Forward, a link).
/// </summary>
public sealed class DeferredNavigationManager : NavigationManager
{
    /// <summary>Creates the manager at <paramref name="uri"/> under <c>http://localhost/</c>.</summary>
    /// <param name="uri">The start location, relative to the base.</param>
    public DeferredNavigationManager(string uri = "library") => Initialize("http://localhost/", "http://localhost/" + uri);

    /// <summary>The navigations asked for, oldest first.</summary>
    public List<(string Uri, NavigationOptions Options)> Requests { get; } = [];

    /// <summary>Reports navigation <paramref name="index"/> back as a location change (the browser carried it out).</summary>
    /// <param name="index">The index in <see cref="Requests"/>.</param>
    public void Echo(int index) => Go(Requests[index].Uri, Requests[index].Options.HistoryEntryState);

    /// <summary>A location change the app did not ask for: Back/Forward to an entry with that state, or a link.</summary>
    /// <param name="uri">The location, relative to the base.</param>
    /// <param name="state">The history entry state, or null.</param>
    public void Go(string uri, string? state = null)
    {
        Uri = ToAbsoluteUri(uri).ToString();
        HistoryEntryState = state;
        NotifyLocationChanged(isInterceptedLink: false);
    }

    /// <inheritdoc />
    protected override void NavigateToCore(string uri, NavigationOptions options) => Requests.Add((uri, options));
}
