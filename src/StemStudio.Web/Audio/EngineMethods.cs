namespace StemStudio.Web.Audio;

/// <summary>
/// JavaScript identifiers of the audio engine contract (<c>src/audio-engine/src/types.ts</c>, v2.3.0; the karaoke methods of 2.1.0 included).
/// Every identifier the wrapper invokes is listed here so a drift test can compare them with the contract.
/// </summary>
internal static class EngineMethods
{
    /// <summary>Supported engine contract version (2.5.0: play-along recording, feature 015; 2.4.0: score follower and loop passes, 014; 2.3.0: practice tempo, 012).</summary>
    public const string ContractVersion = "2.5.0";

    /// <summary>Path of the ES module built by the audio-engine package.</summary>
    public const string ModulePath = "./js/audio-engine.js";

    /// <summary>Dynamic import identifier.</summary>
    public const string Import = "import";

    /// <summary>Module export that creates an engine.</summary>
    public const string CreateEngine = "createEngine";

    /// <summary><c>AudioEngine.loadTrack</c>.</summary>
    public const string LoadTrack = "loadTrack";

    /// <summary><c>AudioEngine.play</c>.</summary>
    public const string Play = "play";

    /// <summary><c>AudioEngine.pause</c>.</summary>
    public const string Pause = "pause";

    /// <summary><c>AudioEngine.stop</c>.</summary>
    public const string Stop = "stop";

    /// <summary><c>AudioEngine.seek</c>.</summary>
    public const string Seek = "seek";

    /// <summary><c>AudioEngine.setChannel</c>.</summary>
    public const string SetChannel = "setChannel";

    /// <summary><c>AudioEngine.setExpanded</c> (contract 2.0.0).</summary>
    public const string SetExpanded = "setExpanded";

    /// <summary><c>AudioEngine.setMaster</c>.</summary>
    public const string SetMaster = "setMaster";

    /// <summary><c>AudioEngine.setLoop</c>.</summary>
    public const string SetLoop = "setLoop";

    /// <summary><c>AudioEngine.setMetronome</c>.</summary>
    public const string SetMetronome = "setMetronome";

    /// <summary><c>AudioEngine.attachMeter</c>.</summary>
    public const string AttachMeter = "attachMeter";

    /// <summary><c>AudioEngine.attachKaraoke</c> (contract 2.1.0).</summary>
    public const string AttachKaraoke = "attachKaraoke";

    /// <summary><c>AudioEngine.setKaraokeOffset</c> (contract 2.1.0).</summary>
    public const string SetKaraokeOffset = "setKaraokeOffset";

    /// <summary><c>AudioEngine.detachKaraoke</c> (contract 2.1.0).</summary>
    public const string DetachKaraoke = "detachKaraoke";

    /// <summary><c>AudioEngine.setPractice</c> (contract 2.3.0).</summary>
    public const string SetPractice = "setPractice";

    /// <summary><c>AudioEngine.getPracticeStatus</c> (contract 2.3.0).</summary>
    public const string GetPracticeStatus = "getPracticeStatus";

    /// <summary><c>AudioEngine.dispose</c>.</summary>
    public const string Dispose = "dispose";

    /// <summary>All <c>AudioEngine</c> instance methods the wrapper invokes.</summary>
    public static readonly IReadOnlyList<string> All =
        [
            LoadTrack, Play, Pause, Stop, Seek, SetChannel, SetExpanded, SetMaster, SetLoop, SetMetronome, AttachMeter,
            AttachKaraoke, SetKaraokeOffset, DetachKaraoke, SetPractice, GetPracticeStatus, Dispose,
        ];
}
