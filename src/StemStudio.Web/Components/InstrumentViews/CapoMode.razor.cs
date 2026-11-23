using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>
/// Capo mode of the guitar family (016 US8, FR-075..FR-078): the setting is kept per track in the browser
/// (<c>stemstudio.chords.capo</c>, data-model 8) and reported through <see cref="OnChanged"/>; the suggestions follow the
/// transpose.
/// </summary>
public sealed partial class CapoMode
{
    private int? _loadedTranspose;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private ViewPreferences Preferences { get; set; } = default!;

    /// <summary>The track.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>The 012 transpose in semitones (suggestions follow it).</summary>
    [Parameter]
    public int TransposeSemitones { get; set; }

    /// <summary>Raised with every change (and once with the stored setting).</summary>
    [Parameter]
    public EventCallback<CapoSetting> OnChanged { get; set; }

    private CapoSetting Setting { get; set; } = CapoSetting.Off;

    private CapoSuggestionsDto? Suggestions { get; set; }

    private string? Error { get; set; }

    private string TrackKey => TrackId.ToString("D", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        var stored = (await StoredAsync()).GetValueOrDefault(TrackKey);
        if (stored is { On: true })
        {
            Setting = stored with { Capo = Math.Clamp(stored.Capo, 0, ContractLimits.CapoMax) };
            await OnChanged.InvokeAsync(Setting);
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (Setting.On && _loadedTranspose != TransposeSemitones)
        {
            await LoadSuggestionsAsync();
        }
    }

    private static string SuggestionText(CapoSuggestionDto suggestion)
    {
        var shapes = suggestion.ShapeKey.Replace(" major", string.Empty, StringComparison.Ordinal).Replace(" minor", "m", StringComparison.Ordinal);
        var percent = (int)Math.Round(suggestion.OpenShare * 100);
        var few = suggestion.FewOpenShapes ? " (few open shapes)" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"Capo {suggestion.Capo} - {shapes} shapes, {percent} % open{few}");
    }

    private async Task LoadSuggestionsAsync()
    {
        _loadedTranspose = TransposeSemitones;
        try
        {
            (Suggestions, Error) = (await Api.GetCapoSuggestionsAsync(TrackId, TransposeSemitones), null);
        }
        catch (ApiException ex)
        {
            (Suggestions, Error) = (null, ex.Error.UserMessage);
        }
    }

    private async Task ToggleAsync()
    {
        await SetAsync(Setting with { On = !Setting.On });
        await (Setting.On ? LoadSuggestionsAsync() : Task.CompletedTask);
    }

    private Task SetCapoAsync(ChangeEventArgs args) =>
        SetAsync(Setting with { Capo = Math.Clamp(int.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var capo) ? capo : 0, 0, ContractLimits.CapoMax) });

    private Task SetSoundingAsync(ChangeEventArgs args) => SetAsync(Setting with { ShowSounding = args.Value is true or "true" or "on" });

    private async Task SetAsync(CapoSetting setting)
    {
        Setting = setting;
        var all = await StoredAsync();
        all[TrackKey] = setting;
        await Preferences.SetAsync(ViewPreferences.CapoKey, all);
        await OnChanged.InvokeAsync(setting);
    }

    private async Task<Dictionary<string, CapoSetting>> StoredAsync() =>
        await Preferences.GetAsync<Dictionary<string, CapoSetting>>(ViewPreferences.CapoKey) ?? [];
}
