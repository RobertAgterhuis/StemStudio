using System.Diagnostics.CodeAnalysis;

namespace StemStudio.Domain.Tracks;

/// <summary>Outcome of <see cref="SourceKey.Parse(string?)"/>: either a key or an actionable error message.</summary>
public sealed record SourceKeyParseResult
{
    private SourceKeyParseResult(SourceKey? key, string? error)
    {
        Key = key;
        Error = error;
    }

    /// <summary>The derived key when the URL was accepted.</summary>
    public SourceKey? Key { get; }

    /// <summary>A message the user can act on when the URL was rejected.</summary>
    public string? Error { get; }

    /// <summary>The canonical URL to forward to the worker when the URL was accepted.</summary>
    public string? CanonicalUrl => Key?.CanonicalUrl;

    /// <summary>True when the URL was accepted.</summary>
    [MemberNotNullWhen(true, nameof(Key))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Key is not null;

    /// <summary>Creates an accepted result.</summary>
    /// <param name="key">The derived key.</param>
    /// <returns>The result.</returns>
    public static SourceKeyParseResult Success(SourceKey key) => new(key, null);

    /// <summary>Creates a rejected result.</summary>
    /// <param name="error">An actionable message.</param>
    /// <returns>The result.</returns>
    public static SourceKeyParseResult Failure(string error) => new(null, error);
}
