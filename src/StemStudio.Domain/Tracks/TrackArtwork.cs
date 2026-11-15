using System.Text.RegularExpressions;

namespace StemStudio.Domain.Tracks;

/// <summary>A picture offered to a track: its source and the SHA-256 of its cover JPEG (feature 005).</summary>
public sealed record ArtworkCandidate
{
    /// <summary>Creates a validated candidate.</summary>
    /// <param name="source">Where the picture came from.</param>
    /// <param name="sha256">SHA-256 of the cover JPEG (64 lower-case hex characters).</param>
    /// <exception cref="DomainValidationException">A value is invalid.</exception>
    public ArtworkCandidate(ArtworkSource source, string sha256)
    {
        ArtworkHash.Validate(source, sha256);
        (Source, Sha256) = (source, sha256);
    }

    /// <summary>Where the picture came from.</summary>
    public ArtworkSource Source { get; }

    /// <summary>SHA-256 of the cover JPEG; the picture version.</summary>
    public string Sha256 { get; }
}

/// <summary>A picture stored for a track (feature 005, data-model 3.1); files live under <c>artwork/{auto,user}/</c>.</summary>
public sealed record TrackArtwork
{
    /// <summary>Length of <see cref="Version"/>.</summary>
    public const int VersionLength = 12;

    /// <summary>Creates a validated record.</summary>
    /// <param name="source">Where the picture came from.</param>
    /// <param name="sha256">SHA-256 of the cover JPEG (64 lower-case hex characters).</param>
    /// <param name="updatedAt">When the picture was stored (UTC).</param>
    /// <exception cref="DomainValidationException">A value is invalid.</exception>
    public TrackArtwork(ArtworkSource source, string sha256, DateTime updatedAt)
    {
        ArtworkHash.Validate(source, sha256);
        (Source, Sha256, UpdatedAt) = (source, sha256, updatedAt);
    }

    /// <summary>Where the picture came from.</summary>
    public ArtworkSource Source { get; }

    /// <summary>SHA-256 of the cover JPEG.</summary>
    public string Sha256 { get; }

    /// <summary>When the picture was stored (UTC).</summary>
    public DateTime UpdatedAt { get; }

    /// <summary>The first 12 hex characters of <see cref="Sha256"/>: the cache-busting version in the picture URLs.</summary>
    public string Version => Sha256[..VersionLength];
}

/// <summary>Validation shared by the artwork records.</summary>
internal static partial class ArtworkHash
{
    /// <summary>Throws unless the source is defined and the hash is 64 lower-case hex characters.</summary>
    /// <param name="source">Source.</param>
    /// <param name="sha256">Hash.</param>
    public static void Validate(ArtworkSource source, string sha256)
    {
        var errors = new ValidationErrors();
        errors.Check(Enum.IsDefined(source), "source", "Must be user, spotify, embedded, coverartarchive or youtube.");
        errors.Check(sha256 is not null && Sha256Pattern().IsMatch(sha256), "sha256", "Must be 64 lower-case hex characters.");
        errors.ThrowIfAny();
    }

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
