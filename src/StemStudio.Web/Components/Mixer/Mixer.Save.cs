using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Api;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Mix save, save-as and loading (<c>?mixId=</c>) of the mixer page (US4, FR-016, FR-017).</summary>
public sealed partial class Mixer
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private bool DialogOpen { get; set; }

    private string DialogName { get; set; } = string.Empty;

    private string? DialogError { get; set; }

    private string? MixError { get; set; }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (State is not null && MixId is { } mixId && mixId != State.MixId)
        {
            await LoadMixAsync(State, mixId);
        }
    }

    private async Task LoadMixAsync(MixerState state, Guid mixId)
    {
        try
        {
            var mix = await Api.GetMixAsync(mixId);
            MixError = mix.TrackId == state.TrackId ? null : MixNames.WrongTrackMessage;
            await (MixError is null ? ApplyMixAsync(state, mix) : Task.CompletedTask);
        }
        catch (ApiException ex)
        {
            MixError = ex.Error.UserMessage;
        }
    }

    /// <summary>Hook of feature 015 (<c>Mixer.Recording.cs</c>): runs after a saved mix was applied to the state and the engine.</summary>
    /// <param name="mix">The loaded mix (its <c>Settings.Take</c> is also in <c>MixerState.Take</c>).</param>
    partial void OnMixLoaded(MixDto mix);

    private async Task ApplyMixAsync(MixerState state, MixDto mix)
    {
        state.ApplyMix(mix);
        await EngineAsync(_ => PushStateAsync(state));
        OnMixLoaded(mix);
    }

    private Task SaveAsync()
    {
        if (State!.MixId is { } id)
        {
            return UpdateAsync(id, State.MixName!);
        }

        OpenSaveAs();
        return Task.CompletedTask;
    }

    private void OpenSaveAs()
    {
        DialogName = State!.MixName ?? string.Empty;
        DialogError = null;
        DialogOpen = true;
    }

    private void CloseDialog() => DialogOpen = false;

    private async Task UpdateAsync(Guid id, string name)
    {
        try
        {
            State!.MarkSaved(await Api.UpdateMixAsync(id, State.ToSettings(name)));
            MixError = null;
        }
        catch (ApiException ex)
        {
            MixError = MixNames.SaveError(ex.Error, name);
        }
    }

    private async Task ConfirmSaveAsAsync(string name)
    {
        DialogError = MixNames.Validate(name);
        if (DialogError is null)
        {
            await CreateAsync(name.Trim());
        }
    }

    // The agent saved this mixer (save_mix_as, update_mix): the same end state as the page's own save.
    private async Task MarkSavedByAgentAsync(Guid mixId)
    {
        var (mix, _) = await ApiCall.TryAsync(() => Api.GetMixAsync(mixId));
        if (mix is null || mix.TrackId != State!.TrackId)
        {
            return;
        }

        State.MarkSaved(mix);
        MixError = null;
        Navigation.NavigateTo($"tracks/{State.TrackId}/mixer?mixId={mix.Id}", replace: true);
        StateHasChanged();
    }

    private async Task CreateAsync(string name)
    {
        try
        {
            var mix = await Api.CreateMixAsync(new CreateMixRequest(State!.TrackId, State.ToSettings(name)));
            State.MarkSaved(mix);
            DialogOpen = false;
            Navigation.NavigateTo($"tracks/{State.TrackId}/mixer?mixId={mix.Id}", replace: true);
        }
        catch (ApiException ex)
        {
            DialogError = MixNames.SaveError(ex.Error, name);
        }
    }
}
