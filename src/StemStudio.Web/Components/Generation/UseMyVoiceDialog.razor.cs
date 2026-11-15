using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;
using StemStudio.Web.Api;
using StemStudio.Web.Generation;
using StemStudio.Web.Voices;

namespace StemStudio.Web.Components.Generation;

/// <summary>Starts a voice conversion of a song take with an AI voice (feature 010, US3).</summary>
public sealed partial class UseMyVoiceDialog
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private GenerationStatusSource StatusSource { get; set; } = default!;

    /// <summary>The song generation.</summary>
    [Parameter, EditorRequired]
    public GenerationDto Generation { get; set; } = default!;

    /// <summary>The take with an AI voice.</summary>
    [Parameter, EditorRequired]
    public GenerationTakeDto Take { get; set; } = default!;

    /// <summary>Cancel or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Raised with the new conversion.</summary>
    [Parameter]
    public EventCallback<GenerationDto> OnStarted { get; set; }

    private IReadOnlyList<VoiceProfileDto>? Profiles { get; set; }

    private Guid? ProfileId { get; set; }

    private int PitchShift { get; set; }

    private bool AutoShift { get; set; } = true;

    private string? Blocked { get; set; }

    private string? Error { get; set; }

    private bool Busy { get; set; }

    private bool CanStart => !Busy && Blocked is null && ProfileId is not null;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        Blocked = VoiceRules.ConversionBlocked(await StatusSource.GetAsync());
        (Profiles, Error) = await ApiCall.TryAsync(() => Api.ListVoiceProfilesAsync());
        ProfileId = Profiles is { Count: > 0 } list ? list[0].Id : null;
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnClose);

    private async Task StartAsync()
    {
        Busy = true;
        var request = new VoiceConversionRequest { ProfileId = ProfileId!.Value, PitchShift = PitchShift, AutoShift = AutoShift };
        Error = await ApiCall.RunAsync(async () => await OnStarted.InvokeAsync(await Api.CreateVoiceConversionAsync(Generation.Id, Take.Index, request)));
        Busy = false;
    }
}
