using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Library;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Components.Library;

/// <summary>Loads a folder view, keeps it fresh while something changes and applies the device's layout and order.</summary>
public sealed partial class FolderView
{
    /// <summary>How often a folder view is polled while it changes (backend API 1.22.0: about once per 2 s).</summary>
    public static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(2);

    private const string MaintenanceCode = "library-maintenance";

    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;
    private bool _maintenance;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private LibraryViewPreferences Preferences { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    /// <summary>The cascaded level and density.</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    /// <summary>The open folder id, or null for the root.</summary>
    [Parameter]
    public string? FolderId { get; set; }

    /// <summary>"Search for the item" in the missing-folder state: the page focuses its search box.</summary>
    [Parameter]
    public EventCallback OnSearchRequested { get; set; }

    private LibraryFolderViewDto? View { get; set; }

    private bool Missing { get; set; }

    private string? Error { get; set; }

    private LibraryViewSettings Settings { get; set; } = LibraryViewSettings.Default;

    private bool Compact => Experience?.Density == DensityPreference.Compact;

    private FolderLevelDto? Level => FolderId is null ? null : new LibraryLocation(FolderId, null, false).Level;

    private string LevelName => Level is { } level ? FolderText.Level(level) : "root";

    private FolderLevelDto? ChildLevel => FolderText.ChildLevel(Level);

    private string Title => View?.Folder?.Name ?? "Library";

    private string ChildrenLabel => ChildLevel switch
    {
        FolderLevelDto.Album => "Albums",
        FolderLevelDto.Song => "Songs",
        _ => "Artists",
    };

    private FolderSortDto DefaultSort => ChildLevel == FolderLevelDto.Album ? FolderSortDto.Year : FolderSortDto.Name;

    private string SortValue => LibraryFolderRequest.Wire(ChildLevel is { } child ? Settings.SortOf(child) ?? DefaultSort : DefaultSort);

    private IEnumerable<(string Value, string Label)> SortOptions => FolderSortOptions.For(ChildLevel);

    /// <inheritdoc />
    protected override bool NeedsPolling =>
        _maintenance || View is { PendingFiling: > 0 } || View?.Items.Any(a => a.Status is AssetStatusDto.Queued or AssetStatusDto.Rendering) == true;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Settings = await Preferences.LoadAsync();
        await LoadAsync(null);
        StartPolling();
    }

    /// <inheritdoc />
    protected override Task RefreshAsync(CancellationToken ct) =>
        Time.GetUtcNow() - _loadedAt >= PollEvery ? LoadAsync(View?.Revision) : Task.CompletedTask;

    private static string Pressed(bool pressed) => pressed ? "true" : "false";

    private string Pressed(FolderLayout layout) => Pressed(Settings.Layout == layout);

    private async Task LoadAsync(string? knownRevision)
    {
        _loadedAt = Time.GetUtcNow();
        if (FolderId is not null && !LibraryLocation.Parse($"{LibraryLocation.RootPath}/{FolderId}").HasValidFolder)
        {
            Missing = true;
            return;
        }

        try
        {
            var view = await FetchAsync(knownRevision);
            (View, Error, _maintenance) = (view ?? View, null, false);
        }
        catch (ApiException ex)
        {
            Fail(ex.Error);
        }
        catch (OperationCanceledException) when (Stopping.IsCancellationRequested)
        {
            // Removed while loading (search results replaced the folder view): nothing to show.
        }
    }

    private Task<LibraryFolderViewDto?> FetchAsync(string? knownRevision)
    {
        var request = new LibraryFolderRequest(ChildLevel is { } child ? Settings.SortOf(child) : null);
        return FolderId is null
            ? Api.GetLibraryRootAsync(request, knownRevision, Stopping)
            : Api.GetLibraryFolderAsync(FolderId, request, knownRevision, Stopping);
    }

    private void Fail(ApiError error)
    {
        if (error.Code == MaintenanceCode)
        {
            _maintenance = true;
            return;
        }

        if (error.IsNotFound)
        {
            (Missing, View) = (true, null);
            return;
        }

        Error = error.UserMessage;
    }

    private Task OnSearchLink() => OnSearchRequested.InvokeAsync();

    private async Task SetLayoutAsync(FolderLayout layout)
    {
        Settings = Settings with { Layout = layout };
        await Preferences.SaveAsync(Settings);
    }

    private async Task OnSortAsync(ChangeEventArgs args)
    {
        if (ChildLevel is { } child && Enum.TryParse<FolderSortDto>(args.Value?.ToString(), true, out var sort) && Enum.IsDefined(sort))
        {
            Settings = Settings.WithSort(child, sort);
            await Preferences.SaveAsync(Settings);
            await LoadAsync(null);
        }
    }

    private async Task ReloadAsync()
    {
        await LoadAsync(null);
        StartPolling();
    }

    private void Replace(AssetDto asset)
    {
        View = View! with { Items = [.. View.Items.Select(a => a.Id == asset.Id ? asset : a)] };
        StartPolling();
    }

    private Task RemoveAsync(AssetDto asset) => LoadAsync(null);
}
