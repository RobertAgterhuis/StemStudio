using System.Text.Json;
using Microsoft.JSInterop;
using StemStudio.Contracts.Library;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Library;

/// <summary>Grid or list view of the folders (feature 022, FR-019).</summary>
public enum FolderLayout
{
    /// <summary>Square tiles (default).</summary>
    Grid,

    /// <summary>Rows with a small picture.</summary>
    List,
}

/// <summary>The device choices of the library: layout and the child order per level (CTO decision 2026-11-17).</summary>
/// <param name="Layout">Grid or list.</param>
/// <param name="Sorts">The chosen order per child level (<c>artist</c>, <c>album</c>, <c>song</c>); absent = default.</param>
public sealed record LibraryViewSettings(FolderLayout Layout, IReadOnlyDictionary<string, FolderSortDto> Sorts)
{
    /// <summary>Grid, default orders.</summary>
    public static LibraryViewSettings Default { get; } = new(FolderLayout.Grid, new Dictionary<string, FolderSortDto>());

    /// <summary>The chosen order of children of a level, or null for the default.</summary>
    /// <param name="childLevel">The level of the children.</param>
    /// <returns>The order, or null.</returns>
    public FolderSortDto? SortOf(FolderLevelDto childLevel) =>
        Sorts.TryGetValue(LibraryFolderRequest.Wire(childLevel), out var sort) ? sort : null;

    /// <summary>The same settings with another order for a child level.</summary>
    /// <param name="childLevel">The level of the children.</param>
    /// <param name="sort">The order.</param>
    /// <returns>The new settings.</returns>
    public LibraryViewSettings WithSort(FolderLevelDto childLevel, FolderSortDto sort) =>
        this with { Sorts = new Dictionary<string, FolderSortDto>(Sorts) { [LibraryFolderRequest.Wire(childLevel)] = sort } };
}

/// <summary>
/// Stores <see cref="LibraryViewSettings"/> in the browser's local storage under <see cref="StorageKey"/> (this device
/// only). Unknown or broken stored values fall back to the defaults.
/// </summary>
/// <param name="js">The JS runtime.</param>
public sealed class LibraryViewPreferences(IJSRuntime js)
{
    /// <summary>The local storage key.</summary>
    public const string StorageKey = "stemstudio.library";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Reads the stored settings.</summary>
    /// <returns>The settings, or the defaults.</returns>
    public async Task<LibraryViewSettings> LoadAsync() => Parse(await BrowserStorage.GetAsync(js, StorageKey));

    /// <summary>Stores the settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>A task that completes when stored (or storage is unavailable).</returns>
    public Task SaveAsync(LibraryViewSettings settings) =>
        BrowserStorage.SetAsync(js, StorageKey, JsonSerializer.Serialize(
            new StoredSettings(settings.Layout == FolderLayout.List ? "list" : "grid", settings.Sorts.ToDictionary(p => p.Key, p => LibraryFolderRequest.Wire(p.Value))), Json));

    private static LibraryViewSettings Parse(string? stored)
    {
        try
        {
            var settings = stored is null ? null : JsonSerializer.Deserialize<StoredSettings>(stored, Json);
            return settings is null ? LibraryViewSettings.Default : new LibraryViewSettings(
                settings.Layout == "list" ? FolderLayout.List : FolderLayout.Grid,
                Sorts(settings.Sorts));
        }
        catch (JsonException)
        {
            return LibraryViewSettings.Default;
        }
    }

    private static Dictionary<string, FolderSortDto> Sorts(Dictionary<string, string>? stored)
    {
        var sorts = new Dictionary<string, FolderSortDto>(StringComparer.Ordinal);
        foreach (var (level, value) in stored ?? [])
        {
            if (Enum.TryParse<FolderLevelDto>(level, true, out var childLevel) && Enum.IsDefined(childLevel)
                && Enum.TryParse<FolderSortDto>(value, true, out var sort) && Enum.IsDefined(sort))
            {
                sorts[LibraryFolderRequest.Wire(childLevel)] = sort;
            }
        }

        return sorts;
    }

    private sealed record StoredSettings(string? Layout, Dictionary<string, string>? Sorts);
}
