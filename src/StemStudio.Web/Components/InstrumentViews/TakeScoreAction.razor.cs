using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>Scores a saved take against the reference melody with the units and tolerance of singing feedback (FR-046).</summary>
public sealed partial class TakeScoreAction
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IInstrumentViewEngine Views { get; set; } = default!;

    [Inject]
    private ViewPreferences Preferences { get; set; } = default!;

    [Inject]
    private IOptions<StemStudioWebOptions> Options { get; set; } = default!;

    /// <summary>The track facts the mixer cascades; nothing is shown without them.</summary>
    [CascadingParameter]
    public SingingSource? Source { get; set; }

    /// <summary>The cascaded level.</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    /// <summary>The take.</summary>
    [Parameter, EditorRequired]
    public TakeItem Item { get; set; } = default!;

    private bool Busy { get; set; }

    private string? Result { get; set; }

    private int? Average { get; set; }

    private async Task ScoreAsync()
    {
        Busy = true;
        try
        {
            await ScoreTakeAsync(Item.Take!);
        }
        catch (Exception ex) when (ex is ApiException or AudioEngineException)
        {
            Result = ex is ApiException api ? api.Error.UserMessage : ex.Message;
        }

        Busy = false;
    }

    private async Task ScoreTakeAsync(RecordingTakeDto take)
    {
        var pitch = await Api.GetVocalPitchAsync(Source!.Track.Id);
        if (pitch is not { Status: AnalysisStatusDto.Ready, Document: { } document })
        {
            Result = "Analyse the reference melody in Singing feedback first.";
            return;
        }

        var result = await Views.ScoreTakeAsync(await RequestAsync(take, document));
        Average = result.Average;
        Result = result.UnitsScored == 0 || result.Average is null
            ? "No line was scored in this take."
            : string.Create(CultureInfo.InvariantCulture, $"Average {result.Average} over {result.UnitsScored} {(result.UnitsScored == 1 ? "line" : "lines")}");
    }

    private async Task<TakeScoreRequest> RequestAsync(RecordingTakeDto take, VocalPitchDocumentDto document)
    {
        var lyrics = await LyricsAsync();
        var level = Experience ?? ExperienceContext.Default;
        return new TakeScoreRequest
        {
            TakeUrl = new Uri(Options.Value.PublicApiBaseUrl, take.AudioUrl).ToString(),
            StartSample = take.StartSample,
            TransposeSemitones = RecordedTakeTranspose,
            Reference = SingingUnits.Reference(document),
            Units = SingingUnits.From(lyrics, Source!.Sections, Source.Track.BeatGrid, Source.Duration).Units,
            ToleranceCents = SingingMix.Tolerance(level, await Preferences.GetAsync<int?>(ViewPreferences.ToleranceKey)),
        };
    }

    // The transpose the take was sung at. The 015 upload rule (RecordingTakeUploadDto.TransposeSemitones "must be 0",
    // else 422) means every saved take was recorded at transpose 0, and RecordingTakeDto carries no transpose yet; once
    // it does, read it from the take here. The mixer's current transpose is deliberately not used.
    private const int RecordedTakeTranspose = 0;

    private async Task<LyricsDto?> LyricsAsync()
    {
        try
        {
            return await Api.GetLyricsAsync(Source!.Track.Id);
        }
        catch (ApiException)
        {
            return null;
        }
    }
}
