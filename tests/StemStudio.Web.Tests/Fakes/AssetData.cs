using StemStudio.Contracts;
using StemStudio.Contracts.Assets;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped library items (backend-api 1.5.0 examples).</summary>
public static class AssetData
{
    public static readonly Guid AssetId = Guid.Parse("3c9a1e7b-5d2f-4a6c-8e0b-1f2a3b4c5d6e");
    public static readonly Guid OtherTrackId = Guid.Parse("55555555-2222-3333-4444-555555555555");

    public static AssetDto Asset(Guid? id = null, AssetStatusDto status = AssetStatusDto.Ready, string name = "Never Gonna Give You Up - bass") => new()
    {
        Id = id ?? AssetId,
        Kind = AssetKindDto.Audio,
        Name = name,
        Status = status,
        Progress = status switch { AssetStatusDto.Ready => 100, AssetStatusDto.Rendering => 42, _ => 0 },
        Error = status == AssetStatusDto.Failed ? "The render failed: the stem file is missing." : null,
        Files = status == AssetStatusDto.Ready ? [new AssetFileDto(AssetFileRoleDto.Audio, "audio/flac", 12_700_000, new string('a', 64), ApiRoutes.AssetFile(id ?? AssetId, ApiRoutes.AssetFileAudio))] : [],
        Audio = status == AssetStatusDto.Ready ? Facts() : null,
        Provenance = Provenance(),
        SizeBytes = status == AssetStatusDto.Ready ? 12_700_000 : 0,
        CreatedAt = TestData.Created,
        UpdatedAt = TestData.Created,
    };

    public static AudioFactsDto Facts(double gain = 0, AudioFileFormatDto format = AudioFileFormatDto.Flac) => new()
    {
        Format = format,
        BitDepth = format == AudioFileFormatDto.Mp3 ? null : 16,
        Mp3BitrateKbps = format == AudioFileFormatDto.Mp3 ? 320 : null,
        DurationSeconds = 213.04,
        PeakDbfs = gain < 0 ? -0.1 : -3.2,
        AppliedGainDb = gain,
    };

    public static ProvenanceDto Provenance(Guid? trackId = null, string title = "Never Gonna Give You Up", bool available = true, string contents = "bass, raw") => new()
    {
        Origin = AssetOriginDto.Export,
        TrackId = trackId ?? TestData.TrackId,
        TrackTitle = title,
        TrackArtist = "Rick Astley",
        TrackAvailable = available,
        Contents = contents,
        Recipe = new ExportRecipeDto
        {
            TrackId = trackId ?? TestData.TrackId,
            Mode = ExportModeDto.Raw,
            Channels = ["bass"],
            Format = new ExportFormatDto(AudioFileFormatDto.Flac, 16),
        },
    };

    public static AssetListDto List(params AssetDto[] items) => new(items, items.Length, items.Sum(i => i.SizeBytes));
}
