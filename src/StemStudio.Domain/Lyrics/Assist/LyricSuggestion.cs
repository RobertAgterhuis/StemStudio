namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>A verified rhyme partner of a device suggestion.</summary>
/// <param name="Line">1-based line of the partner.</param>
/// <param name="Word">The partner word.</param>
public sealed record RhymePartner(int Line, string Word);

/// <summary>What a new suggestion holds (parameter object of <see cref="LyricSuggestion.Create"/>).</summary>
public sealed record SuggestionDraft
{
    /// <summary>Type (the category follows from it).</summary>
    public required SuggestionType Type { get; init; }

    /// <summary>Severity.</summary>
    public required SuggestionSeverity Severity { get; init; }

    /// <summary>Anchor in the assist's revision.</summary>
    public required Anchor Anchor { get; init; }

    /// <summary>Operation.</summary>
    public required SuggestionOperation Operation { get; init; }

    /// <summary>Proposal (null for <see cref="SuggestionOperation.None"/>).</summary>
    public string? Proposal { get; init; }

    /// <summary>Alternatives group.</summary>
    public Guid? GroupId { get; init; }

    /// <summary>Plain-text explanation (1-300).</summary>
    public required string Explanation { get; init; }

    /// <summary>Layer.</summary>
    public required SuggestionSource Source { get; init; }

    /// <summary>Model name of a model suggestion.</summary>
    public string? Model { get; init; }

    /// <summary>Verified syllables before.</summary>
    public int? SyllablesBefore { get; init; }

    /// <summary>Verified syllables after.</summary>
    public int? SyllablesAfter { get; init; }

    /// <summary>Verified rhyme partner.</summary>
    public RhymePartner? RhymeWith { get; init; }

    /// <summary>The category of <see cref="Type"/>.</summary>
    public SuggestionCategory Category => SuggestionTypes.CategoryOf(Type);
}

/// <summary>One proposal of an assist (data-model 4).</summary>
public sealed class LyricSuggestion
{
    /// <summary>Longest explanation.</summary>
    public const int MaxExplanationLength = 300;

    /// <summary>Longest proposal.</summary>
    public const int MaxProposalLength = 400;

    private LyricSuggestion()
    {
        Anchor = new Anchor(0, 0, 0, string.Empty);
        Explanation = string.Empty;
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Assist.</summary>
    public Guid AssistId { get; private set; }

    /// <summary>Sheet.</summary>
    public Guid SheetId { get; private set; }

    /// <summary>Category.</summary>
    public SuggestionCategory Category { get; private set; }

    /// <summary>Type.</summary>
    public SuggestionType Type { get; private set; }

    /// <summary>Severity.</summary>
    public SuggestionSeverity Severity { get; private set; }

    /// <summary>Anchor in the current revision (re-anchored on every save).</summary>
    public Anchor Anchor { get; private set; }

    /// <summary>Operation.</summary>
    public SuggestionOperation Operation { get; private set; }

    /// <summary>Proposal.</summary>
    public string? Proposal { get; private set; }

    /// <summary>Alternatives group.</summary>
    public Guid? GroupId { get; private set; }

    /// <summary>Explanation.</summary>
    public string Explanation { get; private set; }

    /// <summary>Layer.</summary>
    public SuggestionSource Source { get; private set; }

    /// <summary>Model name.</summary>
    public string? Model { get; private set; }

    /// <summary>Syllables before.</summary>
    public int? SyllablesBefore { get; private set; }

    /// <summary>Syllables after.</summary>
    public int? SyllablesAfter { get; private set; }

    /// <summary>Rhyme partner.</summary>
    public RhymePartner? RhymeWith { get; private set; }

    /// <summary>Status.</summary>
    public SuggestionStatus Status { get; private set; }

    /// <summary>The accept action that applied it.</summary>
    public Guid? AcceptGroupId { get; private set; }

    /// <summary>When it was decided (UTC).</summary>
    public DateTime? DecidedAt { get; private set; }

    /// <summary>Creation time (UTC; orders the list).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Creates an open suggestion; texts are cut to their limits.</summary>
    /// <param name="id">Identifier.</param>
    /// <param name="assist">The assist it belongs to.</param>
    /// <param name="draft">The content.</param>
    /// <param name="nowUtc">Current time.</param>
    /// <returns>The suggestion.</returns>
    public static LyricSuggestion Create(Guid id, AssistRun assist, SuggestionDraft draft, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(assist);
        ArgumentNullException.ThrowIfNull(draft);
        return new LyricSuggestion
        {
            Id = id, AssistId = assist.Id, SheetId = assist.SheetId, Category = draft.Category, Type = draft.Type, Severity = draft.Severity,
            Anchor = draft.Anchor, Operation = draft.Operation, Proposal = Cut(draft.Proposal, MaxProposalLength), GroupId = draft.GroupId,
            Explanation = Cut(draft.Explanation, MaxExplanationLength) ?? string.Empty, Source = draft.Source, Model = draft.Model,
            SyllablesBefore = draft.SyllablesBefore, SyllablesAfter = draft.SyllablesAfter, RhymeWith = draft.RhymeWith,
            Status = SuggestionStatus.Open, CreatedAt = nowUtc,
        };
    }

    /// <summary>True when open.</summary>
    public bool IsOpen => Status == SuggestionStatus.Open;

    /// <summary>The suggestion as an applicable change.</summary>
    /// <returns>The applicable suggestion.</returns>
    public ApplicableSuggestion ToApplicable() => new(Id, Anchor, Operation, Proposal, GroupId);

    /// <summary>Moves the anchor (re-anchoring) or marks the suggestion stale.</summary>
    /// <param name="anchor">The new anchor, or null when stale.</param>
    public void Reanchor(Anchor? anchor)
    {
        if (anchor is null)
        {
            Status = SuggestionStatus.Stale;
            return;
        }

        Anchor = anchor;
    }

    /// <summary>Decides the suggestion.</summary>
    /// <param name="status">Accepted, rejected, intentional or superseded.</param>
    /// <param name="acceptGroupId">The accept action (accepted and superseded only; undo reopens them).</param>
    /// <param name="nowUtc">Current time.</param>
    public void Decide(SuggestionStatus status, Guid? acceptGroupId, DateTime nowUtc) =>
        (Status, AcceptGroupId, DecidedAt) = (status, status is SuggestionStatus.Accepted or SuggestionStatus.Superseded ? acceptGroupId : null, nowUtc);

    /// <summary>Opens the suggestion again (undo of its accept, or a stale anchor that is back).</summary>
    /// <param name="anchor">The anchor in the restored text.</param>
    public void Reopen(Anchor anchor) => (Status, Anchor, AcceptGroupId, DecidedAt) = (SuggestionStatus.Open, anchor, null, null);

    private static string? Cut(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
