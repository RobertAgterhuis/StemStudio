using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>POST /api/lyrics/{lyricsId}/assists</c>.</summary>
public sealed record CreateLyricAssistRequest
{
    /// <summary>What to do.</summary>
    public required AssistKindDto Kind { get; init; }

    /// <summary>The revision the editor shows; a different current revision answers <c>409 revision-conflict</c>.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary><c>check</c>: subset of spelling, grammar, flow, licence; <c>devices</c>: subset of device, licence; omitted = the defaults; ignored for writing kinds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SuggestionCategoryDto>? Categories { get; init; }

    /// <summary>First line (1-based, inclusive) of the range; null = whole sheet.</summary>
    public int? LineFrom { get; init; }

    /// <summary>Last line (1-based, inclusive); <c>continue</c> inserts after it.</summary>
    public int? LineTo { get; init; }

    /// <summary>Alternatives 1-5 (3), continuation lines 1-4 (2), titles 1-5 (5); null = the default.</summary>
    public int? Count { get; init; }

    /// <summary>Optional mood or theme hint (at most 200 characters; imitation guard).</summary>
    public string? Instruction { get; init; }
}
