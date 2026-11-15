using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped styles, generations and statuses (backend-api 1.8.0 examples, feature 009).</summary>
public static class GenerationData
{
    public static readonly Guid JazzId = Guid.Parse("5d1c3b2a-0f9e-4d8c-b7a6-958473625140");
    public static readonly Guid SynthwaveId = Guid.Parse("6e2d4c3b-1a0f-4e9d-8c7b-a69584736251");
    public static readonly Guid UserStyleId = Guid.Parse("7f3e5d4c-2b1a-4f0e-9d8c-b7a695847362");
    public static readonly Guid GenerationId = Guid.Parse("8a4f6e5d-3c2b-4a1f-8e9d-c8b7a6958473");
    public static readonly Guid OtherGenerationId = Guid.Parse("9b5a7f6e-4d3c-4b2a-9f8e-d9c8b7a69584");
    public static readonly Guid ImportedTrackId = Guid.Parse("1c6b8a7f-5e4d-4c3b-8a9f-e0d9c8b7a695");

    public static StyleDto Jazz() => new()
    {
        Id = JazzId,
        Kind = StyleKindDto.Preset,
        PresetKey = "jazz-trio",
        Name = "Jazz trio",
        GenreFamily = GenreFamilyDto.Jazz,
        SubGenre = "Small combo",
        TempoFeel = TempoFeelDto.Swing,
        Instrumentation = ["piano", "double bass", "brushed drums"],
        Moods = ["relaxed", "warm"],
        VocalHandling = VocalHandlingDto.Keep,
        DefaultStrength = StrengthDto.Balanced,
        Description = "Acoustic piano trio with brushed drums and a walking bass.",
        CreatedAt = TestData.Created,
        UpdatedAt = TestData.Created,
    };

    public static StyleDto Synthwave() => Jazz() with
    {
        Id = SynthwaveId,
        PresetKey = "synthwave",
        Name = "Synthwave",
        GenreFamily = GenreFamilyDto.Electronic,
        SubGenre = null,
        TempoFeel = TempoFeelDto.Driving,
        Instrumentation = ["analog synth", "drum machine"],
        Moods = ["nostalgic", "energetic"],
        VocalHandling = VocalHandlingDto.Instrumental,
        DefaultStrength = StrengthDto.Strong,
        Description = "Retro synthesizers and a gated drum machine; instrumental.",
    };

    public static StyleDto UserStyle() => Jazz() with
    {
        Id = UserStyleId,
        Kind = StyleKindDto.User,
        PresetKey = null,
        Name = "My bossa",
        GenreFamily = GenreFamilyDto.Latin,
        SubGenre = "Bossa nova",
        TempoFeel = TempoFeelDto.Laidback,
        Instrumentation = ["nylon guitar"],
        Moods = ["dreamy"],
        DefaultStrength = StrengthDto.Light,
        Description = null,
    };

    public static IReadOnlyList<StyleDto> Styles() => [Jazz(), Synthwave(), UserStyle()];

    public static GenerationStatusDto Status(GenerationAvailabilityDto state = GenerationAvailabilityDto.Ready, string? reason = null) => new()
    {
        State = state,
        Reason = reason,
        Models = [new GenerationModelDto { Id = "acestep-v15-turbo", Present = true, Bytes = 4_800_000_000 }],
        Voice = new VoiceServiceStatusDto { State = GenerationAvailabilityDto.NotInstalled, Reason = "The singer service is not installed." },
    };

    public static GenerationStatusDto NotInstalled() => Status(GenerationAvailabilityDto.NotInstalled, "The generator service is not installed.") with { Models = [] };

    public static StyleSnapshotDto Snapshot(StyleDto style) => new()
    {
        StyleId = style.Id,
        Kind = style.Kind,
        PresetKey = style.PresetKey,
        Name = style.Name,
        GenreFamily = style.GenreFamily,
        SubGenre = style.SubGenre,
        TempoFeel = style.TempoFeel,
        Instrumentation = style.Instrumentation,
        Moods = style.Moods,
        VocalHandling = style.VocalHandling,
        DefaultStrength = style.DefaultStrength,
        Description = style.Description,
    };

    public static RestyleDetailsDto Restyle(RestyleModeDto mode = RestyleModeDto.Range) => new()
    {
        SourceTrackId = TestData.TrackId,
        SourceTrackTitle = "Never Gonna Give You Up",
        Range = new SourceRangeDto { StartSeconds = 30, EndSeconds = 75, Mode = mode },
        IncludedStems = ["drums", "bass", "guitar"],
        Style = Snapshot(Jazz()),
        Strength = StrengthDto.Balanced,
        Caption = "jazz, small combo, swing, piano, double bass, brushed drums, relaxed, warm",
    };

    public static GenerationTakeDto Take(int index = 1, Guid? generationId = null) => new()
    {
        Index = index,
        Seed = 4242 + index,
        DurationSeconds = 45,
        AudioUrl = ApiRoutes.TakeAudio(generationId ?? GenerationId, index),
        LibraryAssetId = Guid.Parse("2d7c9b8a-6f5e-4d4c-9b0a-f1e0d9c8b7a6"),
        Model = new GenerationTakeModelDto { Id = "acestep-v15-turbo", Revision = "8f2c1d0" },
    };

    public static GenerationDto Generation(GenerationStateDto status = GenerationStateDto.Completed, Guid? id = null) => new()
    {
        Id = id ?? GenerationId,
        Kind = GenerationKindDto.Restyle,
        Status = status,
        Progress = status switch
        {
            GenerationStateDto.Completed => 100,
            GenerationStateDto.Running => 42,
            _ => 0,
        },
        Stage = status switch
        {
            GenerationStateDto.Running => "Generating take 1 of 1",
            GenerationStateDto.WaitingForGpu => "Waiting for the GPU",
            _ => null,
        },
        Restyle = Restyle(),
        Takes = status == GenerationStateDto.Completed ? [Take(1, id)] : [],
        Device = status == GenerationStateDto.Completed ? new JobDeviceDto(ProcessingDeviceKindDto.Gpu, "NVIDIA GeForce RTX 4070 Ti SUPER", false, null) : null,
        Error = status == GenerationStateDto.Failed
            ? new GenerationErrorDto { Code = GenerationErrorCodeDto.OutOfMemory, Message = "The GPU ran out of memory, also with the low-memory profile." }
            : null,
        CreatedAt = TestData.Created,
        UpdatedAt = TestData.Created,
        CompletedAt = status == GenerationStateDto.Completed ? TestData.Created.AddSeconds(52) : null,
    };
}
