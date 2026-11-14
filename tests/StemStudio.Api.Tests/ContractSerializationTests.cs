using System.Text.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Tests;

/// <summary>Guards the JSON shape of the Contracts DTOs against backend-api.openapi.yaml.</summary>
public sealed class ContractSerializationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TrackDto_SerializesCamelCaseWithLowerCaseEnums()
    {
        var track = new TrackDto
        {
            Id = Guid.Empty,
            Title = "Song",
            SourceType = SourceTypeDto.Youtube,
            SourceKey = "youtube:dQw4w9WgXcQ",
            Status = TrackStatusDto.Analysing,
            Progress = 95,
            Stems = [new StemDto(StemNames.Vocals, ApiRoutes.Stem(Guid.Empty, StemNames.Vocals))],
            BeatGrid = new BeatGridDto(120, [0.5, 1.0], [0.5], 4, 0.9, true),
            CreatedAt = Now,
            UpdatedAt = Now,
        };

        var json = JsonSerializer.Serialize(track, Web);

        json.ShouldContain("\"sourceType\":\"youtube\"");
        json.ShouldContain("\"status\":\"analysing\"");
        json.ShouldContain("\"beatsPerBar\":4");
        json.ShouldContain("\"url\":\"/api/tracks/00000000-0000-0000-0000-000000000000/stems/vocals\"");
    }

    [Fact]
    public void MixDto_RoundTrips()
    {
        var settings = new MixSettingsDto(
            "Practice",
            ContractLimits.DefaultMasterVolume,
            [new ChannelSettingsDto(StemNames.Guitar, ContractLimits.DefaultChannelVolume, -0.5, true, false)],
            new MetronomeSettingsDto(true, ContractLimits.DefaultMetronomeVolume, null, -20, 4),
            new LoopRegionDto(10, 20, true));
        var mix = new MixDto(Guid.NewGuid(), Guid.NewGuid(), settings, Now, Now);

        var copy = JsonSerializer.Deserialize<MixDto>(JsonSerializer.Serialize(mix, Web), Web);

        copy.ShouldNotBeNull();
        copy.Settings.Channels.ShouldBe(settings.Channels);
        copy.Settings.Metronome.ShouldBe(settings.Metronome);
        copy.Settings.Loop.ShouldBe(settings.Loop);
    }

    [Fact]
    public void ApiRoutes_BuildContractPaths()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        ApiRoutes.RetryTrack(id).ShouldBe("/api/tracks/11111111-2222-3333-4444-555555555555/retry");
        ApiRoutes.DuplicateMix(id).ShouldBe("/api/mixes/11111111-2222-3333-4444-555555555555/duplicate");
    }

    [Fact]
    public void StemNames_FallbackIsSubsetOfSixStem()
    {
        StemNames.SixStem.Count.ShouldBe(6);
        StemNames.FourStem.ShouldAllBe(name => StemNames.SixStem.Contains(name));
        ContractLimits.UploadExtensions.ShouldBe([".mp3", ".wav", ".flac"]);
    }
}
