namespace StemStudio.Domain.Voices;

/// <summary>Where a voice sample came from.</summary>
public enum VoiceSampleSource
{
    /// <summary>Recorded in the browser.</summary>
    Recorded,

    /// <summary>Uploaded as a file.</summary>
    Uploaded,
}

/// <summary>The stored, checked sample of a profile.</summary>
/// <param name="Source">Recorded or uploaded.</param>
/// <param name="FilePath">Normalised 48 kHz mono FLAC, relative to the data directory.</param>
/// <param name="PcmSha256">SHA-256 of the decoded PCM (from the singer's check).</param>
/// <param name="FileSha256">SHA-256 of the uploaded bytes.</param>
/// <param name="DurationSeconds">Duration (10-60).</param>
/// <param name="VoicedRatio">Voiced ratio (at least 0.5).</param>
public sealed record VoiceSample(VoiceSampleSource Source, string FilePath, string PcmSha256, string FileSha256, double DurationSeconds, double VoicedRatio);

/// <summary>What a new profile is made of.</summary>
/// <param name="DisplayName">Name (1-60, unique).</param>
/// <param name="Consent">The consent record.</param>
/// <param name="Sample">The sample.</param>
public sealed record VoiceProfileDraft(string DisplayName, VoiceConsent Consent, VoiceSample Sample);

/// <summary>An own-voice profile with its consent record (010 data-model 3.3).</summary>
public sealed class VoiceProfile
{
    /// <summary>Longest display name.</summary>
    public const int MaxDisplayNameLength = 60;

    private VoiceProfile()
    {
        DisplayName = string.Empty;
        NormalizedName = string.Empty;
        Consent = null!;
        SamplePath = string.Empty;
        SamplePcmSha256 = string.Empty;
        SampleFileSha256 = string.Empty;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Display name.</summary>
    public string DisplayName { get; private set; }

    /// <summary>Upper-case name for the unique index.</summary>
    public string NormalizedName { get; private set; }

    /// <summary>The consent record.</summary>
    public VoiceConsent Consent { get; private set; }

    /// <summary>Recorded or uploaded.</summary>
    public VoiceSampleSource SampleSource { get; private set; }

    /// <summary>The sample file, relative to the data directory.</summary>
    public string SamplePath { get; private set; }

    /// <summary>SHA-256 of the decoded PCM.</summary>
    public string SamplePcmSha256 { get; private set; }

    /// <summary>SHA-256 of the uploaded bytes.</summary>
    public string SampleFileSha256 { get; private set; }

    /// <summary>Sample duration.</summary>
    public double DurationSeconds { get; private set; }

    /// <summary>Voiced ratio.</summary>
    public double VoicedRatio { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Creates a profile.</summary>
    /// <param name="id">New identifier.</param>
    /// <param name="draft">Name, consent and sample.</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    /// <returns>The profile.</returns>
    /// <exception cref="DomainValidationException">A rule is broken.</exception>
    public static VoiceProfile Create(Guid id, VoiceProfileDraft draft, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var name = (draft.DisplayName ?? string.Empty).Trim();
        var sample = draft.Sample;
        var errors = new ValidationErrors();
        errors.Check(name.Length is > 0 and <= MaxDisplayNameLength, "displayName", "The name must be 1 to 60 characters.");
        errors.Check(sample.DurationSeconds is >= 10 and <= 60 && sample.VoicedRatio >= 0.5, "sample", "The sample must be 10 to 60 seconds of singing.");
        errors.ThrowIfAny();
        return new VoiceProfile
        {
            Id = id,
            DisplayName = name,
            NormalizedName = VoiceNaming.Normalize(name),
            Consent = draft.Consent,
            SampleSource = sample.Source,
            SamplePath = sample.FilePath,
            SamplePcmSha256 = sample.PcmSha256,
            SampleFileSha256 = sample.FileSha256,
            DurationSeconds = sample.DurationSeconds,
            VoicedRatio = sample.VoicedRatio,
            CreatedAt = nowUtc,
        };
    }

    /// <summary>Gives a profile a merge restore adds the free name of <see cref="VoiceNaming.RestoredName"/>.</summary>
    /// <param name="name">The new name.</param>
    public void RenameRestored(string name) => (DisplayName, NormalizedName) = (name, VoiceNaming.Normalize(name));
}

/// <summary>Voice profile names are unique ignoring case.</summary>
public static class VoiceNaming
{
    /// <summary>The upper-case trimmed name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The normalised name.</returns>
    public static string Normalize(string name) => CopyNames.Normalize(name);

    /// <summary>"&lt;name&gt; (restored)", numbered when taken, at most 60 characters (013 merge, research R-07).</summary>
    /// <param name="name">The archived name.</param>
    /// <param name="takenNames">Names in the library.</param>
    /// <returns>A free name.</returns>
    public static string RestoredName(string name, IEnumerable<string> takenNames) =>
        CopyNames.Next(name, takenNames, VoiceProfile.MaxDisplayNameLength, CopyNames.RestoredWord);
}
