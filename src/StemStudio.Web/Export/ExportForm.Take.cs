using StemStudio.Contracts.Assets;

namespace StemStudio.Web.Export;

/// <summary>
/// Feature 015 part of the export form (FR-015, US6): the take presets. "Take only" sends the raw take (mode raw, channels
/// <c>["take"]</c>, <c>takeId</c>; the backend trims to the take's span when no range is given); "Take + mix" sends the mixer
/// state with <c>mix.take</c> and <c>takeId</c>. Without a take the 007 request is unchanged.
/// </summary>
public sealed partial class ExportForm
{
    /// <summary>The chosen take preset (default "Take + mix", what the user hears).</summary>
    public TakeExportPreset TakePreset { get; set; } = TakeExportPreset.TakeMix;

    /// <summary>True when the take is still being saved (export disabled, <c>export-take-saving</c>).</summary>
    public bool TakeSaving => Target.Take is { Ready: false };

    private partial CreateAudioExportRequest WithTake(CreateAudioExportRequest request) => Target.Take is not { } take
        ? request
        : TakePreset == TakeExportPreset.TakeOnly
            ? request with { Mode = ExportModeDto.Raw, Channels = [ExportTarget.TakeChannel], Mix = null, TakeId = take.TakeId }
            : request with { TakeId = take.TakeId };
}
