using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using StemStudio.Contracts;
using StemStudio.Contracts.Voices;
using StemStudio.Web.Api;
using StemStudio.Web.Interop;
using StemStudio.Web.Voices;

namespace StemStudio.Web.Components.Voices;

/// <summary>Collects an own-voice sample and the consent, and creates the profile (feature 010, US3).</summary>
public sealed partial class NewVoicePanel
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IVoiceRecorder Recorder { get; set; } = default!;

    /// <summary>Why profiles cannot be saved now (singer not installed), or null.</summary>
    [Parameter]
    public string? SaveBlockedReason { get; set; }

    /// <summary>Raised with the new profile.</summary>
    [Parameter]
    public EventCallback<VoiceProfileDto> OnSaved { get; set; }

    /// <summary>Cancel pressed.</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    private VoiceDraft Draft { get; } = new();

    private InputFile? Input { get; set; }

    private string? Problem { get; set; }

    private bool Busy { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        (Draft.Statement, var error) = await ApiCall.TryAsync(() => Api.GetVoiceConsentAsync());
        Problem = error is null ? null : $"The consent statement could not be loaded ({error}); a voice profile needs it. Reload the page.";
    }

    private void OnRecorded(RecordedSample sample) =>
        Draft.Sample = new VoiceSample(sample.Bytes, "recording.webm", sample.ContentType, VoiceSampleSourceDto.Recorded, sample.PreviewUrl);

    private async Task UploadAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;
        Problem = VoiceRules.FileProblem(file.Name, file.Size);
        if (Problem is null)
        {
            using var bytes = new MemoryStream();
            await using (var source = file.OpenReadStream(ContractLimits.MaxVoiceSampleBytes))
            {
                await source.CopyToAsync(bytes);
            }

            var preview = await Recorder.FileUrlAsync(Input!.Element!.Value);
            Draft.Sample = new VoiceSample(bytes.ToArray(), file.Name, file.ContentType, VoiceSampleSourceDto.Uploaded, preview);
        }
    }

    private async Task SaveAsync()
    {
        Busy = true;
        var upload = Draft.Upload();
        await using (upload.Sample.Content)
        {
            var (profile, error) = await ApiCall.TryAsync(() => Api.CreateVoiceProfileAsync(upload));
            Problem = error;
            Busy = false;
            await (profile is null ? Task.CompletedTask : OnSaved.InvokeAsync(profile));
        }
    }
}
