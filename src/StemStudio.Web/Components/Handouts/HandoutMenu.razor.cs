using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Handouts;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Handouts;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Handouts;

/// <summary>
/// The handout menu button (WAI-ARIA APG menu button: Enter, Space and ArrowDown open at the first item, ArrowUp at the
/// last; arrows, Home and End move; Escape closes and returns the focus to the button; Tab closes). The entries are the
/// seven families and, at Advanced and Expert, "Customise..." (null in <see cref="Entries"/>).
/// </summary>
public sealed partial class HandoutMenu
{
    private readonly ElementReference[] _items = new ElementReference[HandoutText.Families.Count + 1];
    private readonly MenuButtonState _menu = new();
    private ElementReference _button;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The cascaded level (Advanced without a shell).</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    /// <summary>Song or setlist.</summary>
    [Parameter, EditorRequired]
    public HandoutSourceDto Source { get; set; } = default!;

    /// <summary>Test-id prefix: <c>handout</c> (track page) or <c>setlist-handout</c> (setlist page).</summary>
    [Parameter]
    public string Prefix { get; set; } = "handout";

    /// <summary>A create link of a missing ingredient was chosen in the dialog; null hides the links.</summary>
    [Parameter]
    public EventCallback<IngredientRequest> OnIngredientAction { get; set; }

    private bool IsSetlist => Prefix != "handout";

    private string Title => IsSetlist ? "Lesson handout per instrument" : "Lesson handout";

    private string HelpId => IsSetlist ? "setlist-handout-menu" : "handout-menu";

    private string MenuId => $"{Prefix}-menu-list";

    private IReadOnlyList<HandoutFamilyDto?> Entries =>
        [.. HandoutText.Families.Cast<HandoutFamilyDto?>(), .. CanCustomise ? new HandoutFamilyDto?[] { null } : []];

    private bool CanCustomise => (Experience ?? ExperienceContext.Default).IsVisible("handout.customise");

    private bool Open => _menu.Open;

    private bool Busy { get; set; }

    private string? Error { get; set; }

    private HandoutCreated? Saved { get; set; }

    private IReadOnlyList<string> Missing { get; set; } = [];

    private bool Customising { get; set; }

    private HandoutFamilyDto CustomiseFamily { get; set; } = HandoutFamilyDto.Overview;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_menu.TakeFocusPending())
        {
            await FocusOnce.TryAsync(Open ? _items[_menu.Focus] : _button);
        }
    }

    private string EntryId(HandoutFamilyDto? family) => family is { } f ? $"{Prefix}-family-{HandoutText.Wire(f)}" : $"{Prefix}-customise";

    private static string EntryText(HandoutFamilyDto? family) => family is { } f ? HandoutText.Family(f) : "Customise...";

    private void Toggle() => _menu.Toggle();

    private void ButtonKey(KeyboardEventArgs args) => _menu.ButtonKey(args.Key, Entries.Count);

    private void MenuKey(KeyboardEventArgs args) => _menu.MenuKey(args.Key, Entries.Count);

    private Task ChooseAsync(HandoutFamilyDto? family)
    {
        _menu.Close();
        if (family is not { } chosen)
        {
            Customising = true;
            return Task.CompletedTask;
        }

        return CreateAsync(chosen);
    }

    private async Task CreateAsync(HandoutFamilyDto family)
    {
        (Busy, Error, Saved, Missing) = (true, null, null, []);
        var (asset, error) = await ApiCall.TryAsync(() => Api.CreateHandoutAsync(new CreateHandoutRequest { Source = Source, Family = family }));
        Error = error;
        Saved = asset is null ? null : new HandoutCreated(asset, family);
        Missing = asset is null ? [] : await MissingAsync(family);
        Busy = false;
    }

    // The default parts that are not there yet (FR-010); a failing options read only hides the list.
    private async Task<IReadOnlyList<string>> MissingAsync(HandoutFamilyDto family)
    {
        var (options, _) = await ApiCall.TryAsync(() => Api.GetHandoutOptionsAsync(Source, family));
        return options is null ? [] : HandoutText.Missing(options);
    }

    private void CloseDialog() => Customising = false;

    private Task OnDialogCreatedAsync(HandoutCreated created)
    {
        (Customising, Saved, Missing, Error) = (false, created, [], null);
        return Task.CompletedTask;
    }
}
