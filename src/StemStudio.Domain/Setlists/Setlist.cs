namespace StemStudio.Domain.Setlists;

/// <summary>An ordered list of songs with notes (014 FR-015, data-model 3.6).</summary>
public sealed class Setlist
{
    private List<SetlistEntry> _entries = [];

    private Setlist()
    {
        Name = string.Empty;
        Note = string.Empty;
    }

    /// <summary>Setlist id.</summary>
    public Guid Id { get; private set; }

    /// <summary>Name (1..80, trimmed).</summary>
    public string Name { get; private set; }

    /// <summary>Note (0..2,000).</summary>
    public string Note { get; private set; }

    /// <summary>Entries removed by track deletions since the UI last showed the notice.</summary>
    public int RemovedEntries { get; private set; }

    /// <summary>Created (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Last change (UTC).</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>The entries in play order.</summary>
    public IReadOnlyList<SetlistEntry> Entries => _entries;

    /// <summary>Creates an empty setlist.</summary>
    /// <param name="id">Id.</param>
    /// <param name="name">Name.</param>
    /// <param name="note">Note (null = empty).</param>
    /// <param name="nowUtc">Now.</param>
    /// <returns>The setlist.</returns>
    /// <exception cref="DomainValidationException">Name or note is invalid.</exception>
    public static Setlist Create(Guid id, string name, string? note, DateTime nowUtc)
    {
        var setlist = new Setlist { Id = id, CreatedAt = nowUtc };
        setlist.Update(name ?? string.Empty, note ?? string.Empty, nowUtc);
        return setlist;
    }

    /// <summary>Renames and/or changes the note; null leaves a field unchanged.</summary>
    /// <param name="name">New name, or null.</param>
    /// <param name="note">New note, or null.</param>
    /// <param name="nowUtc">Now.</param>
    /// <exception cref="DomainValidationException">Name or note is invalid.</exception>
    public void Update(string? name, string? note, DateTime nowUtc)
    {
        var trimmed = name?.Trim();
        var errors = new ValidationErrors();
        SetlistRules.ValidateHeader(errors, trimmed, note);
        errors.ThrowIfAny();
        (Name, Note, UpdatedAt) = (trimmed ?? Name, note ?? Note, nowUtc);
    }

    /// <summary>Replaces the whole ordered entry list; given ids of own entries are kept, other ids get new ones.</summary>
    /// <param name="entries">The new list.</param>
    /// <param name="context">The library.</param>
    /// <param name="newId">Id source.</param>
    /// <param name="nowUtc">Now.</param>
    /// <exception cref="DomainValidationException">A rule is broken.</exception>
    public void ReplaceEntries(IReadOnlyList<SetlistEntryInput> entries, SetlistEntryContext context, Func<Guid> newId, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(newId);
        var errors = SetlistRules.ValidateEntries(entries, context);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        var existing = _entries.ToDictionary(entry => entry.Id);
        _entries = entries.Select((input, position) => Merge(existing, input, position, newId)).ToList();
        UpdatedAt = nowUtc;
    }

    /// <summary>Counts entries a track deletion removed.</summary>
    /// <param name="count">Removed entries.</param>
    public void CountRemoved(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        RemovedEntries += count;
    }

    /// <summary>Resets the removed-entries counter after the UI showed the notice.</summary>
    /// <returns>True when the counter changed.</returns>
    public bool AcknowledgeRemoved()
    {
        var changed = RemovedEntries != 0;
        RemovedEntries = 0;
        return changed;
    }

    private SetlistEntry Merge(Dictionary<Guid, SetlistEntry> existing, SetlistEntryInput input, int position, Func<Guid> newId)
    {
        if (input.Id is { } id && existing.Remove(id, out var kept))
        {
            kept.Apply(position, input);
            return kept;
        }

        return SetlistEntry.Create(newId(), Id, position, input);
    }
}
