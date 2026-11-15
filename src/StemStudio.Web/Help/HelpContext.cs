using Microsoft.AspNetCore.Components;
using StemStudio.Web.Interop;

namespace StemStudio.Web.Help;

/// <summary>Red stub (T048).</summary>
/// <param name="catalog">Catalog.</param>
/// <param name="interop">Interop.</param>
/// <param name="navigation">Navigation.</param>
public sealed class HelpContext(IHelpCatalog catalog, IWebInterop interop, NavigationManager navigation)
{
    /// <summary>Changed.</summary>
    public event Action? Changed;

    /// <summary>Open.</summary>
    public bool IsOpen => Changed is null && catalog is null && interop is null && navigation is null;

    /// <summary>Topic.</summary>
    public string? TopicId { get; }

    /// <summary>Back.</summary>
    public bool CanGoBack { get; }

    /// <summary>Open.</summary>
    /// <param name="topicId">Topic.</param>
    /// <returns>Task.</returns>
    public Task OpenAsync(string? topicId = null) => Task.CompletedTask;

    /// <summary>Screen.</summary>
    /// <returns>Task.</returns>
    public Task OpenScreenAsync() => Task.CompletedTask;

    /// <summary>Show.</summary>
    /// <param name="topicId">Topic.</param>
    /// <returns>Task.</returns>
    public Task ShowAsync(string topicId) => Task.CompletedTask;

    /// <summary>Toggle.</summary>
    /// <returns>Task.</returns>
    public Task ToggleAsync() => Task.CompletedTask;

    /// <summary>Close.</summary>
    /// <returns>Task.</returns>
    public Task CloseAsync() => Task.CompletedTask;

    /// <summary>Back.</summary>
    public void Back()
    {
    }
}
