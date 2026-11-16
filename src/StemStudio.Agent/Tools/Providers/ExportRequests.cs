using StemStudio.Agent.Ports;
using StemStudio.Contracts.Assets;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>Builds export requests from validated arguments.</summary>
internal static class ExportRequests
{
    public static async Task<CreateAudioExportRequest> AudioAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var mix = call.Text("mode") == "mix";
        var mixId = call.Id("mixId");
        var settings = mix && mixId is null ? await MixProjections.ReadOpenMixerAsync(call, cancellationToken) : null;
        return new CreateAudioExportRequest
        {
            TrackId = call.RequiredId("trackId"),
            Mode = mix ? ExportModeDto.Mix : ExportModeDto.Raw,
            Channels = call.Strings("channels") is { Count: > 0 } channels ? channels : null,
            MixId = mix ? mixId : null,
            Mix = settings is null ? null : new ExportMixDto(settings.MasterVolume, settings.Channels) { Expanded = settings.Expanded, DetailChannels = settings.DetailChannels },
            Range = Range(call),
            Format = Format(call),
            Name = call.Text("name"),
        };
    }

    public static ExportFormatDto Format(ToolInvocation call)
    {
        var format = call.Text("format") switch
        {
            "wav" => AudioFileFormatDto.Wav,
            "mp3" => AudioFileFormatDto.Mp3,
            _ => AudioFileFormatDto.Flac,
        };
        return format == AudioFileFormatDto.Mp3 ? new ExportFormatDto(format, null, Allowed(call.Whole("mp3BitrateKbps"), 192, 256, 320)) : new ExportFormatDto(format, Allowed(call.Whole("bitDepth"), 16, 24));
    }

    private static RangeDto? Range(ToolInvocation call) =>
        call.Arguments.TryGetProperty("range", out var range)
            ? new RangeDto(range.GetProperty("startSeconds").GetDouble(), range.GetProperty("endSeconds").GetDouble())
            : null;

    private static int? Allowed(int? value, params int[] allowed) =>
        value is null || allowed.Contains(value.Value) ? value : throw new AgentActionException($"Use one of {string.Join(", ", allowed)}.");
}

/// <summary>Output size estimates of exports (FR-024: exports above 500 MB need approval).</summary>
internal static class ExportSizes
{
    /// <summary>Above this estimate an export is heavy.</summary>
    public const long HeavyBytes = 500_000_000;

    private const double SampleRate = 44_100;
    private const double FlacRatio = 0.6;
    private const double DefaultSeconds = 240;
    private const int DetailFactor = 2;
    private const int StandardStems = 6;

    /// <summary>True when the estimated output exceeds <see cref="HeavyBytes"/>.</summary>
    /// <param name="call">The call.</param>
    /// <param name="files">Number of files; null = one per stem (stem pack).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public static async Task<bool> AboveLimitAsync(ToolInvocation call, int? files, CancellationToken cancellationToken) =>
        await BytesAsync(call, files, cancellationToken) > HeavyBytes;

    /// <summary>The approval card's estimate.</summary>
    public static async Task<ToolEstimate> EstimateAsync(ToolInvocation call, int? files, CancellationToken cancellationToken)
    {
        var megabytes = await BytesAsync(call, files, cancellationToken) / 1_000_000;
        return new ToolEstimate($"Export of about {megabytes} MB to the library", null, "CPU");
    }

    private static async Task<long> BytesAsync(ToolInvocation call, int? files, CancellationToken cancellationToken)
    {
        var track = await call.Actions.Tracks.GetTrackAsync(call.RequiredId("trackId"), cancellationToken);
        var seconds = Seconds(call) ?? track.DurationSeconds ?? DefaultSeconds;
        var count = files ?? (track.Stems.Count > 0 ? track.Stems.Count : StandardStems) * (call.Flag("includeDetail") == true ? DetailFactor : 1);
        return (long)(seconds * BytesPerSecond(ExportRequests.Format(call)) * count);
    }

    private static double? Seconds(ToolInvocation call) => call.Arguments.TryGetProperty("range", out var range)
        ? range.GetProperty("endSeconds").GetDouble() - range.GetProperty("startSeconds").GetDouble()
        : null;

    private static double BytesPerSecond(ExportFormatDto format) => format.Format switch
    {
        AudioFileFormatDto.Mp3 => (format.Mp3BitrateKbps ?? 320) * 125,
        AudioFileFormatDto.Wav => SampleRate * 2 * (format.BitDepth ?? 16) / 8,
        _ => SampleRate * 2 * (format.BitDepth ?? 16) / 8 * FlacRatio,
    };
}
