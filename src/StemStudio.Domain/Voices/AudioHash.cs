namespace StemStudio.Domain.Voices;

/// <summary>What kind of stored audio a hash belongs to (010 exact-copy guard).</summary>
public enum AudioHashKind
{
    /// <summary>A stem.</summary>
    Stem,

    /// <summary>A detail stem.</summary>
    DetailStem,

    /// <summary>A generated take.</summary>
    Take,

    /// <summary>The converted vocal of a take.</summary>
    TakeVocal,

    /// <summary>An exported library file.</summary>
    Export,

    /// <summary>An uploaded source file.</summary>
    Upload,
}

/// <summary>The file-byte SHA-256 of audio StemStudio stored; a voice sample with such a hash is refused (010 data-model 3.3).</summary>
/// <param name="Sha256">64 lower-case hex characters.</param>
/// <param name="Kind">The kind of file.</param>
/// <param name="OwnerId">The track, generation or asset that owns the file.</param>
public sealed record AudioHash(string Sha256, AudioHashKind Kind, Guid OwnerId);
