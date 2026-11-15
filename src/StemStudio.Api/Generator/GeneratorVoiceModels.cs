namespace StemStudio.Api.Generator;

/// <summary>Song parameters (<c>SongParams</c>, generator 1.1.0).</summary>
/// <param name="Caption">Caption (1-512).</param>
/// <param name="Lyrics">Lyrics, or <c>[Instrumental]</c>.</param>
/// <param name="Instrumental">True for an instrumental.</param>
/// <param name="VocalLanguage">ISO 639-1 or <c>unknown</c>.</param>
/// <param name="Bpm">Tempo, or null for the model's choice.</param>
/// <param name="KeyScale">Key, or null.</param>
/// <param name="TimeSignature">Beats per bar, or null.</param>
/// <param name="DurationSeconds">Length.</param>
/// <param name="Seeds">One seed per take.</param>
public sealed record GeneratorSongParams(
    string Caption,
    string Lyrics,
    bool Instrumental,
    string VocalLanguage,
    int? Bpm,
    string? KeyScale,
    string? TimeSignature,
    double DurationSeconds,
    IReadOnlyList<int> Seeds)
{
    /// <summary>Diffusion steps (turbo: 8).</summary>
    public int InferenceSteps { get; init; } = 8;

    /// <summary>Shift (turbo: 3.0).</summary>
    public double Shift { get; init; } = 3.0;

    /// <summary>Run the language-model planner.</summary>
    public bool Thinking { get; init; } = true;
}

/// <summary>Voice-conversion parameters (<c>VoiceConversionParams</c>, generator 1.1.0).</summary>
/// <param name="PitchShift">Semitones (-12..12).</param>
/// <param name="AutoShift">Shift to the prompt's register.</param>
/// <param name="ProfileName">Written into <c>AI_VOICE_PROFILE</c>.</param>
public sealed record GeneratorVoiceConversionParams(int PitchShift, bool AutoShift, string ProfileName)
{
    /// <summary>Diffusion steps (default 32).</summary>
    public int Steps { get; init; } = 32;

    /// <summary>Guidance (default 3.0).</summary>
    public double Cfg { get; init; } = 3.0;

    /// <summary>Vocal gain in the remix (default 0 dB).</summary>
    public double VocalGainDb { get; init; }
}

/// <summary>A voice-conversion job: parameters, the vocal stem, the profile sample and 0-5 accompaniment stems (FLAC paths).</summary>
/// <param name="Params">The parameters.</param>
/// <param name="TargetPath">The vocal stem.</param>
/// <param name="PromptPath">The voice sample.</param>
/// <param name="AccompanimentPaths">The other stems.</param>
public sealed record GeneratorVoiceConversionRequest(GeneratorVoiceConversionParams Params, string TargetPath, string PromptPath, IReadOnlyList<string> AccompanimentPaths);

/// <summary>The result of <c>POST /v1/voice-samples/check</c>.</summary>
/// <param name="CheckId">Check id (32 hex).</param>
/// <param name="DurationSeconds">Duration.</param>
/// <param name="VoicedRatio">Voiced ratio.</param>
/// <param name="PcmSha256">SHA-256 of the decoded PCM.</param>
/// <param name="Ok">Usable.</param>
/// <param name="Reason"><c>tooShort</c>, <c>tooLong</c> or <c>unvoiced</c> when not ok.</param>
/// <param name="Message">Readable message when not ok.</param>
public sealed record GeneratorSampleCheck(string CheckId, double DurationSeconds, double VoicedRatio, string PcmSha256, bool Ok, string? Reason, string? Message);

/// <summary>The singer service: the same contract as the generator, served at <c>StemStudio:SingerBaseUrl</c> (010 ADR-019).</summary>
public interface ISingerClient : IGeneratorClient;

/// <summary>The generator client bound to the singer service.</summary>
/// <param name="http">Client whose base address is the singer URL.</param>
/// <param name="logger">Logger.</param>
/// <param name="clock">Clock.</param>
public sealed class SingerClient(HttpClient http, ILogger<GeneratorClient> logger, TimeProvider clock) : GeneratorClient(http, logger, clock), ISingerClient;
