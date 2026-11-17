using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Mixer;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Components.Mixer.Practice;

/// <summary>
/// The practice tempo panel; values come from <see cref="PracticeRules"/>, changes go to the mixer page. Open or closed:
/// the browser's stored choice (<see cref="PracticePanelChoice"/>), else open only while something is not neutral.
/// </summary>
public sealed partial class PracticePanel
{
    private bool? _choice;

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    /// <summary>The mixer's practice settings.</summary>
    [Parameter, EditorRequired]
    public PracticeSettingsDto Settings { get; set; } = PracticeSettingsDto.Default;

    /// <summary>The engine's last snapshot.</summary>
    [Parameter]
    public PracticeStatus Status { get; set; } = PracticeStatus.Initial;

    /// <summary>The track tempo (manual BPM or reliable grid), or null.</summary>
    [Parameter]
    public double? TrackBpm { get; set; }

    /// <summary>Raised with a requested speed.</summary>
    [Parameter]
    public EventCallback<int> OnSpeed { get; set; }

    /// <summary>Raised with a requested transpose.</summary>
    [Parameter]
    public EventCallback<int> OnTranspose { get; set; }

    /// <summary>"Original speed and key" pressed.</summary>
    [Parameter]
    public EventCallback OnReset { get; set; }

    /// <summary>Count-in and speed trainer controls.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private IReadOnlyList<string> Summary => PracticePanelChoice.Summary(HeardSpeed, HeardTranspose, Settings);

    private bool Expanded => _choice ?? PracticePanelChoice.IsOpenByDefault(Summary);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync() =>
        _choice = PracticePanelChoice.Parse(await BrowserStorage.GetAsync(Js, PracticePanelChoice.StorageKey));

    private Task ToggleAsync()
    {
        var expanded = !Expanded;
        _choice = expanded;
        return BrowserStorage.SetAsync(Js, PracticePanelChoice.StorageKey, PracticePanelChoice.Serialize(expanded));
    }

    private int HeardSpeed => PracticeRules.HeardSpeed(Settings, Status);

    private int HeardTranspose => PracticeRules.HeardTranspose(Settings, Status);

    private bool TrainerRuns => PracticeRules.TrainerRuns(Status);

    private bool Unavailable => Status.Stretch == StretchState.Unavailable;

    private bool Loading => Status.Stretch == StretchState.Loading;

    // Disabled when both are already at the original values (the trainer and the count-in are not part of the reset).
    private bool ResetDisabled => Unavailable || Settings is { SpeedPercent: 100, TransposeSemitones: 0 };

    private int? EffectiveBpm => PracticeRules.EffectiveBpm(TrackBpm, HeardSpeed);
}
