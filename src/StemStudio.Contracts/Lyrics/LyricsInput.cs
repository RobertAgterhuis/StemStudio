using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Lyrics;

/// <summary>Body of <c>POST /api/lyrics</c> and <c>PUT /api/lyrics/{lyricsId}</c> (1.9.0).</summary>
public sealed record LyricsInput
{
    /// <summary>Title, 1-80 characters.</summary>
    public required string Title { get; init; }

    /// <summary>Language code (<see cref="ContractLimits.LyricsLanguages"/>).</summary>
    public required string Language { get; init; }

    /// <summary>Raw text; 1-4,096 characters after normalisation.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// 1.23.0 (feature 023), optional: the revision the client edited; a different current revision answers
    /// <c>409 revision-conflict</c>. Null (not written) = 010 behaviour, last write wins.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpectedRevision { get; init; }
}
