using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped sheet music data (backend-api 1.7.0 examples).</summary>
public static class ScoreData
{
    public static readonly Guid ScoreId = Guid.Parse("9e8d7c6b-5a4f-4e3d-8c2b-1a0f9e8d7c6b");

    public const string Attribution = "Piano transcription model: Kong et al., ByteDance (CC BY 4.0).";

    private static readonly ScoreProfileDto[] Pitched = [ScoreProfileDto.Vocals, ScoreProfileDto.Bass, ScoreProfileDto.Guitar, ScoreProfileDto.Keys, ScoreProfileDto.Piano];

    /// <summary>The <c>BassScoreOptions</c> example (CPU worker, reliable grid).</summary>
    public static ScoreOptionsDto Options(string channel = "bass", ScoreProfileDto? suggested = ScoreProfileDto.Bass, GridStatusDto grid = GridStatusDto.Reliable) => new()
    {
        Source = AudioSourceDto.FromChannel(TestData.TrackId, channel),
        DurationSeconds = 262,
        SuggestedProfile = suggested,
        AllowedProfiles = Pitched,
        GridStatus = grid,
        DefaultQuality = ScoreQualityDto.Fast,
        Estimates =
        [
            new(ScoreProfileDto.Vocals, null, 31), new(ScoreProfileDto.Bass, null, 31), new(ScoreProfileDto.Guitar, null, 31),
            new(ScoreProfileDto.Keys, null, 31), new(ScoreProfileDto.Piano, ScoreQualityDto.Accurate, 515), new(ScoreProfileDto.Piano, ScoreQualityDto.Fast, 31),
        ],
        Limits =
        [
            new(ScoreProfileDto.Vocals, "One melody line; ornaments, slides and breath noises are simplified or missed."),
            new(ScoreProfileDto.Bass, "One note at a time; written an octave higher (bass clef 8vb); slides and ghost notes are missed."),
            new(ScoreProfileDto.Guitar, "Chords are notated as stacked notes without fingering or tablature; strums can add extra notes."),
            new(ScoreProfileDto.Keys, "Two staves split at middle C; pedal and voicing are not notated."),
            new(ScoreProfileDto.Piano, "Two staves split at middle C; the accurate model is slow on the CPU."),
        ],
        Attribution = Attribution,
    };

    /// <summary>The <c>DrumsScoreOptions</c> example (drums not split yet).</summary>
    public static ScoreOptionsDto DrumOptions(bool requireSplit = true) => Options("drums", ScoreProfileDto.Drums) with
    {
        AllowedProfiles = [ScoreProfileDto.Drums],
        DrumsRequireSplit = requireSplit,
        Estimates = [new(ScoreProfileDto.Drums, null, 10)],
        Limits = [new(ScoreProfileDto.Drums, "Kick, snare, toms, hi-hat and cymbals on one staff; bleed between the detail stems can add extra hits.")],
    };

    /// <summary>A score item; ready ones carry the three files and the facts of the <c>ReadyScoreAsset</c> example.</summary>
    public static AssetDto Asset(AssetStatusDto status = AssetStatusDto.Ready, ScoreFactsDto? facts = null) => new()
    {
        Id = ScoreId,
        Kind = AssetKindDto.Score,
        Name = "Never Gonna Give You Up - bass (sheet music)",
        Status = status,
        Progress = status == AssetStatusDto.Ready ? 100 : 0,
        Files = status == AssetStatusDto.Ready
            ? [File(AssetFileRoleDto.Pdf, "pdf", "application/pdf"), File(AssetFileRoleDto.MusicXml, "musicxml", "application/vnd.recordare.musicxml+xml"), File(AssetFileRoleDto.Midi, "midi", "audio/midi")]
            : [],
        Score = status == AssetStatusDto.Ready ? facts ?? Facts() : null,
        Provenance = AssetData.Provenance(contents: "bass, sheet music") with { Origin = AssetOriginDto.Transcription, Recipe = null },
        SizeBytes = status == AssetStatusDto.Ready ? 602_387 : 0,
        CreatedAt = TestData.Created,
        UpdatedAt = TestData.Created,
    };

    /// <summary>The facts of the ready bass score.</summary>
    public static ScoreFactsDto Facts(ScoreProfileDto profile = ScoreProfileDto.Bass) => new()
    {
        Profile = profile,
        Engine = ScoreEngineDto.BasicPitch,
        Key = profile == ScoreProfileDto.Drums ? null : new KeyInfoDto("A-", KeyModeDto.Major, 0.83, KeySourceDto.Estimated),
        Bpm = 118,
        TimeSignature = "4/4",
        Bars = 125,
        Notes = 386,
        Pages = 3,
        Resolution = ScoreResolutionDto.Sixteenth,
        Warnings = [new ScoreWarningDto(ScoreWarningCodeDto.NotesCleaned, "87 very short or out-of-range notes were removed.")],
        Device = new JobDeviceDto(ProcessingDeviceKindDto.Cpu, null, false, null),
    };

    private static AssetFileDto File(AssetFileRoleDto role, string path, string type) =>
        new(role, type, 1000, new string('b', 64), $"/api/assets/{ScoreId}/files/{path}");
}
