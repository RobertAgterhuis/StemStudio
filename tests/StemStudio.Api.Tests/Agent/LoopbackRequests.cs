using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;

namespace StemStudio.Api.Tests.Agent;

/// <summary>Valid request bodies for the loopback action tests.</summary>
internal static class LoopbackRequests
{
    public static CreateAudioExportRequest AudioExport(Guid trackId) => new()
    {
        TrackId = trackId,
        Mode = ExportModeDto.Raw,
        Format = new ExportFormatDto(AudioFileFormatDto.Flac),
    };

    public static CreateStemPackRequest StemPack(Guid trackId) => new(trackId, new ExportFormatDto(AudioFileFormatDto.Wav, 16));

    public static CreateScoreRequest Score(Guid trackId) => new() { Source = AudioSourceDto.FromChannel(trackId, "bass") };
}
