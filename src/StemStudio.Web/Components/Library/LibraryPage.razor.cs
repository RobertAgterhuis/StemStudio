using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Library;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Components.Library;

/// <summary>
/// Route host of the library: follows the URL (folder, <c>?q=</c>, <c>?view=all</c>), owns the live search and its
/// announcer, and binds <c>/</c> to the search box while a library page is open (feature 022, FR-022..FR-027, T069).
/// </summary>
public sealed partial class LibraryPage
{
    /// <summary>The command of the <c>/</c> key in scope <see cref="ShortcutScope.Library"/> (web-interop 1.2.0).</summary>
    public const string FocusSearchCommand = "library.search.focus";

    private LibrarySearchBox? _box;
    private SearchResults? _results;
    private IAsyncDisposable? _shortcuts;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    [Inject]
    private ShortcutKeys Keys { get; set; } = default!;

    /// <summary>The cascaded level.</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    /// <summary>The open folder (<c>/library/{folderId}</c>), or null at the root.</summary>
    [Parameter]
    public string? FolderId { get; set; }

    /// <summary>The item to highlight (<c>?asset=</c>, from confirmations before 1.22.0): opens "All items".</summary>
    [SupplyParameterFromQuery(Name = "asset")]
    public Guid? Highlight { get; set; }

    private LibraryLocation Location { get; set; } = LibraryLocation.Root;

    private LiveSearchController Search { get; set; } = default!;

    private LibraryAnnouncer Announcer { get; set; } = default!;

    private bool ShowAllItems => Highlight is not null || (Location.AllItems && (Experience ?? ExperienceContext.Default).IsVisible("library.filters"));

    private string Title => Search.IsActive ? "Search the library" : "Library";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        Search.Changed -= OnSearchChanged;
        Announcer.Changed -= OnAnnounced;
        Search.Dispose();
        Announcer.Dispose();
        await (_shortcuts?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Search = new LiveSearchController(
            new LiveSearchPorts((request, ct) => Api.SearchLibraryAsync(request, ct), Navigate, FocusSearchAsync, InvokeAsync),
            Time);
        Announcer = new LibraryAnnouncer(Time, InvokeAsync);
        Search.Changed += OnSearchChanged;
        Announcer.Changed += OnAnnounced;
        Navigation.LocationChanged += OnLocationChanged;
    }

    /// <inheritdoc />
    protected override Task OnParametersSetAsync()
    {
        Location = LibraryLocation.Parse(Navigation.Uri);
        return Search.StartAsync(Location);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _shortcuts = await Keys.RegisterAsync(ShortcutScope.Library, command => InvokeAsync(() => OnCommandAsync(command)));
        }
    }

    private Task OnCommandAsync(string command) =>
        command == FocusSearchCommand ? FocusSearchAsync().AsTask() : Task.CompletedTask;

    private ValueTask FocusSearchAsync() => _box?.FocusAsync() ?? ValueTask.CompletedTask;

    private Task FocusFirstResultAsync() => _results?.FocusFirstAsync() ?? Task.CompletedTask;

    private Task ShowAllAsync() => Search.ShowAllAsync();

    private void Navigate(string url, bool replace) => Navigation.NavigateTo(url, new NavigationOptions { ReplaceHistoryEntry = replace });

    // Back/forward and links change the URL without new parameters when the route stays the same.
    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => _ = InvokeAsync(async () =>
    {
        var location = LibraryLocation.Parse(args.Location);
        if (location != Location)
        {
            Location = location;
            await Search.StartAsync(location);
            StateHasChanged();
        }
    });

    private void OnSearchChanged()
    {
        Announcer.Schedule(Search.IsActive ? Search.Result : null);
        StateHasChanged();
    }

    private void OnAnnounced() => StateHasChanged();
}
