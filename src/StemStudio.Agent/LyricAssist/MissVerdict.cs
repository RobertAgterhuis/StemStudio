namespace StemStudio.Agent.LyricAssist;

/// <summary>The model's verdict on a dictionary miss (<c>misses[].verdict</c> of lyric-assist-output 1.0.0).</summary>
public enum MissVerdict
{
    /// <summary>A misspelling; <see cref="ModelMissVerdict.Correction"/> holds the fix.</summary>
    Typo,

    /// <summary>Deliberate poetic licence; <see cref="ModelMissVerdict.LicenceType"/> names it.</summary>
    Licence,

    /// <summary>A correct word the dictionary does not know (a name, a neologism); nothing is reported.</summary>
    Keep,
}
