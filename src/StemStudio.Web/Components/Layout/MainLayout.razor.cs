using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using StemStudio.Web.Agent;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.LibraryCare;
using StemStudio.Web.Practice;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Layout;

/// <summary>
/// The app shell: loads the preferences once after the first render, registers the global shortcuts, re-renders
/// on every preference, help or toast change, cascades the effective <see cref="ExperienceContext"/> and turns
/// in-app help links (<c>/help/&lt;id&gt;</c>) into help panel navigation. It also starts the library care status polling
/// (013) for the job indicator and the maintenance overlay.
/// </summary>
public sealed partial class MainLayout : IAsyncDisposable
{
    private IAsyncDisposable? _shortcuts;

    [Inject]
    private PreferencesState Preferences { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    [Inject]
    private HelpContext Help { get; set; } = default!;

    [Inject]
    private ShortcutKeys Keys { get; set; } = default!;

    [Inject]
    private ShellOverlays Overlays { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private AgentPanelState Agent { get; set; } = default!;

    [Inject]
    private LibraryCareStatusSource LibraryCare { get; set; } = default!;

    private ExperienceContext Experience => new(Preferences.Current.EffectiveLevel, Preferences.Current.EffectiveDensity);

    private string LevelName => Lower(Preferences.Current.EffectiveLevel);

    private string DensityName => Lower(Preferences.Current.EffectiveDensity);

    private string ShellClass => "ss-shell" + (Help.IsOpen ? " ss-shell-help-open" : string.Empty) + (Agent.IsOpen ? " ss-shell-agent-open" : string.Empty);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Preferences.Changed -= OnChanged;
        Toasts.Changed -= OnChanged;
        Help.Changed -= OnChanged;
        Overlays.Changed -= OnChanged;
        Agent.Changed -= OnChanged;
        LibraryCare.Changed -= OnChanged;
        await (_shortcuts?.DisposeAsync() ?? ValueTask.CompletedTask);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Preferences.Changed += OnChanged;
        Toasts.Changed += OnChanged;
        Help.Changed += OnChanged;
        Overlays.Changed += OnChanged;
        Agent.Changed += OnChanged;
        LibraryCare.Changed += OnChanged;
        LibraryCare.Start();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Preferences.LoadAsync();
            _shortcuts = await Keys.RegisterAsync(ShortcutScope.Global, id => InvokeAsync(() => OnCommandAsync(id)));
        }
    }

    private static string Lower<T>(T value)
        where T : struct, Enum => value.ToString().ToLowerInvariant();

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    private Task OnCommandAsync(string commandId) => commandId switch
    {
        "help.open" => Help.OpenAsync(),
        "palette.open" => Run(Overlays.OpenPalette),
        "shortcuts.open" => Run(OpenShortcutsByKey),
        _ when commandId.StartsWith("go.", StringComparison.Ordinal) => Run(() => Go(commandId)),
        _ => Task.CompletedTask,
    };

    private static Task Run(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    // Mod+/ opens the overview at Expert only; other levels reach it from the help panel (FR-017).
    private void OpenShortcutsByKey()
    {
        if (Experience.IsVisible("shell.shortcut-overlay-key"))
        {
            Overlays.OpenShortcuts();
        }
    }

    private void Go(string commandId) => Navigation.NavigateTo(commandId switch
    {
        "go.tracks" => "tracks",
        "go.mixes" => "mixes",
        _ => string.Empty,
    });

    private async Task InterceptHelpLinkAsync(LocationChangingContext context)
    {
        if (HelpLinks.TopicFromUri(context.TargetLocation) is { } topicId)
        {
            context.PreventNavigation();
            await Help.ShowAsync(topicId);
        }
    }

}
