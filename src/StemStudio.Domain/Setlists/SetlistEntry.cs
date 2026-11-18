namespace StemStudio.Domain.Setlists;

/// <summary>One song of a setlist (data-model 3.6). Changed only through <see cref="Setlist"/>.</summary>
public sealed class SetlistEntry
{
    private SetlistEntry()
    {
        Note = string.Empty;
    }

    /// <summary>Entry id (kept across replaces).</summary>
    public Guid Id { get; private set; }

    /// <summary>The setlist.</summary>
    public Guid SetlistId { get; private set; }

    /// <summary>Play order 0..99.</summary>
    public int Position { get; private set; }

    /// <summary>The song.</summary>
    public Guid TrackId { get; private set; }

    /// <summary>Optional mix of the song (null after the mix is deleted).</summary>
    public Guid? MixId { get; private set; }

    /// <summary>Note (0..1,000).</summary>
    public string Note { get; private set; }

    internal static SetlistEntry Create(Guid id, Guid setlistId, int position, SetlistEntryInput input)
    {
        var entry = new SetlistEntry { Id = id, SetlistId = setlistId };
        entry.Apply(position, input);
        return entry;
    }

    internal void Apply(int position, SetlistEntryInput input) =>
        (Position, TrackId, MixId, Note) = (position, input.TrackId, input.MixId, SetlistRules.CleanNote(input.Note));
}
