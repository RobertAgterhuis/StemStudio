using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.LibraryCare;

namespace StemStudio.Web.Components.Care;

/// <summary>The two tabs of Library care.</summary>
public enum CareTab
{
    /// <summary>Backups, restore and the previous library.</summary>
    Backups,

    /// <summary>Storage overview and clean-up.</summary>
    Storage,
}

/// <summary>Selects the tab from <c>?tab=</c>, starts the status polling and re-renders on every status change.</summary>
public sealed partial class LibraryCarePage
{
    private ElementReference _backupsTab;
    private ElementReference _storageTab;

    [Inject]
    private LibraryCareStatusSource StatusSource { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary><c>?tab=storage</c> opens the Storage tab; anything else the Backups tab.</summary>
    [SupplyParameterFromQuery(Name = "tab")]
    public string? TabQuery { get; set; }

    private CareTab Tab { get; set; }

    private string TabName => Tab == CareTab.Storage ? "storage" : "backups";

    /// <inheritdoc />
    public void Dispose() => StatusSource.Changed -= OnStatusChanged;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        StatusSource.Changed += OnStatusChanged;
        StatusSource.Start();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() =>
        Tab = string.Equals(TabQuery, "storage", StringComparison.OrdinalIgnoreCase) ? CareTab.Storage : CareTab.Backups;

    private string Selected(CareTab tab) => Tab == tab ? "true" : "false";

    private int TabIndex(CareTab tab) => Tab == tab ? 0 : -1;

    private void OnStatusChanged() => _ = InvokeAsync(StateHasChanged);

    private void Select(CareTab tab)
    {
        if (Tab != tab)
        {
            Tab = tab;
            Navigation.NavigateTo(tab == CareTab.Storage ? "care?tab=storage" : "care", new NavigationOptions { ReplaceHistoryEntry = true });
        }
    }

    // Arrow keys, Home and End move between the tabs and select them (WAI-ARIA tabs pattern, automatic activation).
    private async Task OnTabKeyAsync(KeyboardEventArgs args)
    {
        CareTab? target = args.Key switch
        {
            "ArrowRight" or "ArrowLeft" => Tab == CareTab.Backups ? CareTab.Storage : CareTab.Backups,
            "Home" => CareTab.Backups,
            "End" => CareTab.Storage,
            _ => null,
        };
        if (target is { } tab)
        {
            Select(tab);
            await (tab == CareTab.Storage ? _storageTab : _backupsTab).FocusAsync();
        }
    }
}
