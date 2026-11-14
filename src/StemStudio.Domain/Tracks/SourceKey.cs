namespace StemStudio.Domain.Tracks;

/// <summary>
/// Cache identity of a track's source: <c>youtube:&lt;videoId&gt;</c>, <c>spotify:&lt;trackId&gt;</c> or
/// <c>upload:&lt;sha256&gt;</c> (data-model.md, FR-009).
/// </summary>
public sealed record SourceKey
{
    private SourceKey(SourceType type, string id)
    {
        Type = type;
        Id = id;
    }

    /// <summary>Source type encoded in the key.</summary>
    public SourceType Type { get; }

    /// <summary>The identifier part (video ID, track ID or lower-case SHA-256 hex).</summary>
    public string Id { get; }

    /// <summary>The full key, e.g. <c>youtube:dQw4w9WgXcQ</c>.</summary>
    public string Value => $"{Prefix(Type)}:{Id}";

    /// <summary>The canonical URL forwarded to the worker; null for uploads.</summary>
    public string? CanonicalUrl => Type switch
    {
        SourceType.Youtube => $"https://www.youtube.com/watch?v={Id}",
        SourceType.Spotify => $"https://open.spotify.com/track/{Id}",
        _ => null,
    };

    /// <summary>Validates a user-supplied URL and derives its key (see source-url-vectors.json).</summary>
    /// <param name="url">The URL as typed by the user; surrounding whitespace is ignored.</param>
    /// <returns>The key and canonical URL, or an actionable error message.</returns>
    public static SourceKeyParseResult Parse(string? url) => SourceUrlParser.Parse(url);

    /// <summary>Builds the key of an uploaded file.</summary>
    /// <param name="sha256">SHA-256 of the file bytes as 64 lower-case hex characters.</param>
    /// <returns>The <c>upload:&lt;sha256&gt;</c> key.</returns>
    /// <exception cref="ArgumentException">The hash is not 64 lower-case hex characters.</exception>
    public static SourceKey ForUpload(string sha256) => Create(SourceType.Upload, sha256);

    /// <summary>Restores a key from its stored <see cref="Value"/>.</summary>
    /// <param name="value">A key such as <c>spotify:4cOdK2wGLETKBW3PvgPWqT</c>.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentException">The value is not a valid key.</exception>
    public static SourceKey FromStored(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var separator = value.IndexOf(':', StringComparison.Ordinal);
        var type = separator < 0 ? null : TypeFromPrefix(value[..separator]);
        if (type is null)
        {
            throw new ArgumentException($"'{value}' is not a valid source key.", nameof(value));
        }

        return Create(type.Value, value[(separator + 1)..]);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Creates a key after validating the identifier for its type.</summary>
    /// <param name="type">Source type.</param>
    /// <param name="id">Identifier part.</param>
    /// <returns>The key.</returns>
    internal static SourceKey Create(SourceType type, string id)
    {
        if (!SourceIds.IsValid(type, id))
        {
            throw new ArgumentException($"'{id}' is not a valid {Prefix(type)} identifier.", nameof(id));
        }

        return new SourceKey(type, id);
    }

    private static string Prefix(SourceType type) => type.ToString().ToLowerInvariant();

    private static SourceType? TypeFromPrefix(string prefix) => prefix switch
    {
        "youtube" => SourceType.Youtube,
        "spotify" => SourceType.Spotify,
        "upload" => SourceType.Upload,
        _ => null,
    };
}
