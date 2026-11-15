using StemStudio.Contracts;
using StemStudio.Contracts.Assets;

namespace StemStudio.Web.Export;

/// <summary>
/// The choices of the export dialog and the request they produce (feature 007, FR-001/002/004/006/013). Format details,
/// range and the channel selection are only changed at Advanced and Expert, so at Beginner they keep their defaults
/// (null = the backend default of data-model 4.2).
/// </summary>
/// <param name="target">What is exported.</param>
public sealed class ExportForm(ExportTarget target)
{
    /// <summary>Message for a name longer than <see cref="ContractLimits.AssetNameMaxLength"/>.</summary>
    public const string NameMessage = "Enter a name of at most 120 characters, or leave it empty.";

    private AudioFileFormatDto _format = AudioFileFormatDto.Flac;

    /// <summary>What is exported.</summary>
    public ExportTarget Target { get; } = target;

    /// <summary>Output format; changing it resets the format details.</summary>
    public AudioFileFormatDto Format
    {
        get => _format;
        set => (_format, BitDepth, Mp3Bitrate) = (value, null, null);
    }

    /// <summary>Bit depth for FLAC/WAV, or null for the default (16).</summary>
    public int? BitDepth { get; set; }

    /// <summary>MP3 bit rate, or null for the default (320).</summary>
    public int? Mp3Bitrate { get; set; }

    /// <summary>Mix mode: true for "Selected channels", false for "What I hear".</summary>
    public bool SelectedChannels { get; set; }

    /// <summary>The selected channel ids (preset to the audible channels).</summary>
    public HashSet<string> Selected { get; } = [.. target.Audible];

    /// <summary>The range inputs (seconds as typed).</summary>
    public RangeInput Range { get; } = new(target.Loop, target.Duration);

    /// <summary>The entered name; empty = the default name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Stem pack: also save the kept detail stems.</summary>
    public bool IncludeDetail { get; set; }

    /// <summary>False while "Selected channels" has no channel (submit disabled).</summary>
    public bool CanSubmit => !(Target.Kind == ExportKind.Mix && SelectedChannels && Selected.Count == 0);

    /// <summary>Checks the name and the range.</summary>
    /// <returns>The first readable problem, or null.</returns>
    public string? Validate() => Name.Trim().Length > ContractLimits.AssetNameMaxLength ? NameMessage : Range.Validate();

    /// <summary>The format as sent (details only when chosen).</summary>
    /// <returns>The format.</returns>
    public ExportFormatDto FormatDto() => new(Format, BitDepth, Mp3Bitrate);

    /// <summary>The request of a stem or mix export (call <see cref="Validate"/> first).</summary>
    /// <returns>The request.</returns>
    public CreateAudioExportRequest AudioRequest() => new()
    {
        TrackId = Target.Track.Id,
        Mode = Target.Kind == ExportKind.Stem ? ExportModeDto.Raw : ExportModeDto.Mix,
        Channels = Channels(),
        Mix = Target.Mix,
        Range = Range.ToDto(),
        Format = FormatDto(),
        Name = Name.Trim().Length == 0 ? null : Name.Trim(),
    };

    /// <summary>The request of a stem pack.</summary>
    /// <returns>The request.</returns>
    public CreateStemPackRequest PackRequest() => new(Target.Track.Id, FormatDto(), IncludeDetail && Target.HasDetail);

    private List<string>? Channels() => Target.Kind switch
    {
        ExportKind.Stem => [Target.Channel!],
        _ when SelectedChannels => [.. Target.Channels.Select(c => c.Id).Where(Selected.Contains)],
        _ => null,
    };
}
