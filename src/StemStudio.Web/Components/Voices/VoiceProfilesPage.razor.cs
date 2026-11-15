using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StemStudio.Contracts.Voices;
using StemStudio.Web.Api;
using StemStudio.Web.Generation;
using StemStudio.Web.Voices;

namespace StemStudio.Web.Components.Voices;

/// <summary>Loads the voice profiles, adds new ones and deletes them with or without their takes (feature 010, US3).</summary>
public sealed partial class VoiceProfilesPage
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private GenerationStatusSource StatusSource { get; set; } = default!;

    [Inject]
    private IOptions<StemStudioWebOptions> Options { get; set; } = default!;

    private List<VoiceProfileDto>? Items { get; set; }

    private string? Error { get; set; }

    private string? ActionError { get; set; }

    private string? SaveBlocked { get; set; }

    private bool Creating { get; set; }

    private VoiceProfileDto? Deleting { get; set; }

    private bool DeleteTakes { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        SaveBlocked = VoiceRules.SaveBlocked(await StatusSource.GetAsync());
        (var profiles, Error) = await ApiCall.TryAsync(() => Api.ListVoiceProfilesAsync());
        Items = profiles is null ? null : [.. profiles];
    }

    private static string TakesLabel(VoiceProfileDto profile) => profile.ConvertedTakeCount == 1
        ? "Also delete the 1 take converted to this voice"
        : string.Create(CultureInfo.InvariantCulture, $"Also delete the {profile.ConvertedTakeCount} takes converted to this voice");

    private string Url(string relative) => new Uri(Options.Value.PublicApiBaseUrl, relative).ToString();

    private void OnSaved(VoiceProfileDto profile)
    {
        Items?.Insert(0, profile);
        Creating = false;
    }

    private void AskDelete(VoiceProfileDto profile) => (Deleting, DeleteTakes) = (profile, false);

    private async Task DeleteAsync()
    {
        var (profile, deleteTakes) = (Deleting!, DeleteTakes);
        Deleting = null;
        ActionError = await ApiCall.RunAsync(async () =>
        {
            await Api.DeleteVoiceProfileAsync(profile.Id, deleteTakes);
            Items!.RemoveAll(p => p.Id == profile.Id);
        });
    }
}
