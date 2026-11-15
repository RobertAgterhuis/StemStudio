using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Help;

/// <summary>
/// State of the help panel (data-model.md 4, FR-023): open/closed, the shown topic (null = the help index), a
/// Back history, focus capture on open and restore on close. Context resolution: focused element -> closest
/// <c>data-help-id</c> -> its topic, else the topic of the current screen. While open, the panel follows
/// navigation to the new screen's topic. Scoped per circuit.
/// </summary>
public sealed class HelpContext : IDisposable
{
    private readonly IHelpCatalog _catalog;
    private readonly IWebInterop _interop;
    private readonly NavigationManager _navigation;
    private readonly Stack<string?> _history = new();
    private IAsyncDisposable? _focus;
    private bool _following;

    /// <summary>Creates the context; it starts following navigation when first opened (inside a circuit).</summary>
    /// <param name="catalog">Help topics.</param>
    /// <param name="interop">Focus helpers.</param>
    /// <param name="navigation">The current location.</param>
    public HelpContext(IHelpCatalog catalog, IWebInterop interop, NavigationManager navigation)
    {
        _catalog = catalog;
        _interop = interop;
        _navigation = navigation;
    }

    /// <summary>Raised after every change (open, close, topic, navigation).</summary>
    public event Action? Changed;

    /// <summary>True while the panel is open.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The shown topic id, or <see langword="null"/> for the help index.</summary>
    public string? TopicId { get; private set; }

    /// <summary>True when Back has somewhere to go.</summary>
    public bool CanGoBack => _history.Count > 0;

    /// <summary>The screen topic of the current location, if any.</summary>
    public string? ScreenTopicId
    {
        get
        {
            var path = "/" + _navigation.ToBaseRelativePath(_navigation.Uri).Split('?', '#')[0].TrimEnd('/');
            return _catalog.Topics.FirstOrDefault(t => t.Route is { } route && RouteMatches(route, path))?.Id;
        }
    }

    /// <summary>Opens the panel at a topic, or, without one, at the focused control's or the screen's topic.</summary>
    /// <param name="topicId">An explicit topic; unknown ids show the index.</param>
    /// <returns>A task that completes when open.</returns>
    public async Task OpenAsync(string? topicId = null)
    {
        var resolved = topicId is null ? await ContextTopicAsync() : Known(topicId);
        await OpenAtAsync(resolved);
    }

    /// <summary>Opens the panel at the current screen's topic (help button, rail Help item).</summary>
    /// <returns>A task that completes when open.</returns>
    public Task OpenScreenAsync() => OpenAtAsync(ScreenTopicId);

    /// <summary>Help button: opens at the screen topic, or closes when open.</summary>
    /// <returns>A task that completes when done.</returns>
    public Task ToggleAsync() => IsOpen ? CloseAsync() : OpenScreenAsync();

    /// <summary>Shows a topic inside the panel (links, results, related); opens the panel when closed.</summary>
    /// <param name="topicId">The topic id.</param>
    /// <returns>A task that completes when shown.</returns>
    public async Task ShowAsync(string topicId)
    {
        if (!IsOpen)
        {
            await OpenAtAsync(Known(topicId));
            return;
        }

        _history.Push(TopicId);
        TopicId = Known(topicId);
        Changed?.Invoke();
    }

    /// <summary>Returns to the previous topic, if any.</summary>
    public void Back()
    {
        if (_history.TryPop(out var previous))
        {
            TopicId = previous;
            Changed?.Invoke();
        }
    }

    /// <summary>Closes the panel and returns focus to where it was.</summary>
    /// <returns>A task that completes when closed.</returns>
    public async Task CloseAsync()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        _history.Clear();
        Changed?.Invoke();
        var focus = _focus;
        _focus = null;
        await (focus?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_following)
        {
            _navigation.LocationChanged -= OnLocationChanged;
        }
    }

    /// <summary>True when an app path matches a route template such as <c>/tracks/{id}/mixer</c>.</summary>
    /// <param name="template">The route template.</param>
    /// <param name="path">The path, starting with <c>/</c>.</param>
    /// <returns>Whether the template matches.</returns>
    internal static bool RouteMatches(string template, string path)
    {
        var pattern = "^" + Regex.Replace(Regex.Escape(template.TrimEnd('/')), @"\\\{[^}]+}", "[^/]+") + "/?$";
        return Regex.IsMatch(path, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    private string? Known(string topicId) => _catalog.Topic(topicId)?.Id;

    private async Task<string?> ContextTopicAsync()
    {
        var focused = await _interop.FocusedHelpIdAsync();
        return (focused is null ? null : _catalog.TopicForAnchor(focused)?.Id) ?? ScreenTopicId;
    }

    private async Task OpenAtAsync(string? topicId)
    {
        if (!IsOpen)
        {
            _focus = await _interop.CaptureFocusAsync();
        }

        Follow();
        IsOpen = true;
        _history.Clear();
        TopicId = topicId;
        Changed?.Invoke();
    }

    private void Follow()
    {
        if (!_following)
        {
            _following = true;
            _navigation.LocationChanged += OnLocationChanged;
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (!IsOpen)
        {
            return;
        }

        _history.Clear();
        TopicId = ScreenTopicId;
        Changed?.Invoke();
    }
}
