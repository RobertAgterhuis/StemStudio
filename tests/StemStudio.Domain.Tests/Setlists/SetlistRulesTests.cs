using StemStudio.Domain.Setlists;

namespace StemStudio.Domain.Tests.Setlists;

/// <summary>Setlists and their entries (014 FR-015, data-model 3.6).</summary>
public sealed class SetlistRulesTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 18, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrackA = Guid.NewGuid();
    private static readonly Guid TrackB = Guid.NewGuid();
    private static readonly Guid MixOfA = Guid.NewGuid();
    private static readonly Guid MixOfB = Guid.NewGuid();

    private static readonly SetlistEntryContext Library = new(
        new HashSet<Guid> { TrackA, TrackB },
        new Dictionary<Guid, Guid> { [MixOfA] = TrackA, [MixOfB] = TrackB });

    private static Setlist NewSetlist() => Setlist.Create(Guid.NewGuid(), "  Friday rehearsal ", "Bring the capo.", T0);

    private static SetlistEntryInput Entry(Guid track, Guid? mix = null, string note = "", Guid? id = null) => new(id, track, mix, note);

    [Fact]
    public void Create_TrimsTheName_AndStartsEmpty()
    {
        var setlist = NewSetlist();

        (setlist.Name, setlist.Note, setlist.Entries.Count, setlist.RemovedEntries, setlist.CreatedAt, setlist.UpdatedAt).ShouldBe(("Friday rehearsal", "Bring the capo.", 0, 0, T0, T0));
    }

    [Theory]
    [InlineData("", "name")]
    [InlineData("   ", "name")]
    [InlineData("123456789012345678901234567890123456789012345678901234567890123456789012345678901", "name")]
    public void Create_RefusesBadNames(string name, string field) =>
        Should.Throw<DomainValidationException>(() => Setlist.Create(Guid.NewGuid(), name, "", T0)).Errors.Single().Field.ShouldBe(field);

    [Fact]
    public void Create_RefusesALongNote_AndANullNoteIsEmpty()
    {
        Should.Throw<DomainValidationException>(() => Setlist.Create(Guid.NewGuid(), "Set", new string('n', 2001), T0)).Errors.Single().Field.ShouldBe("note");
        Setlist.Create(Guid.NewGuid(), "Set", new string('n', 2000), T0).Note.Length.ShouldBe(2000);
        Setlist.Create(Guid.NewGuid(), "Set", null, T0).Note.ShouldBe("");
    }

    [Fact]
    public void Update_ChangesOnlyTheGivenFields()
    {
        var setlist = NewSetlist();

        setlist.Update(null, "New note", T0.AddHours(1));
        (setlist.Name, setlist.Note, setlist.UpdatedAt).ShouldBe(("Friday rehearsal", "New note", T0.AddHours(1)));
        setlist.Update(" Gig ", null, T0.AddHours(2));
        (setlist.Name, setlist.Note).ShouldBe(("Gig", "New note"));
        Should.Throw<DomainValidationException>(() => setlist.Update("", null, T0));
        setlist.Name.ShouldBe("Gig");
    }

    [Fact]
    public void ReplaceEntries_KeepsGivenIds_InPlayOrder_AndAllowsDuplicateTracks()
    {
        var setlist = NewSetlist();
        setlist.ReplaceEntries([Entry(TrackA, MixOfA, "Start soft."), Entry(TrackB)], Library, Guid.NewGuid, T0.AddMinutes(1));
        var (first, second) = (setlist.Entries[0], setlist.Entries[1]);

        setlist.ReplaceEntries([Entry(TrackB, id: second.Id), Entry(TrackA, note: "Encore"), Entry(TrackA, MixOfA, " Again ", first.Id)], Library, Guid.NewGuid, T0.AddMinutes(2));

        setlist.Entries.Select(entry => entry.Position).ShouldBe([0, 1, 2]);
        setlist.Entries.Select(entry => entry.TrackId).ShouldBe([TrackB, TrackA, TrackA]);
        setlist.Entries[0].Id.ShouldBe(second.Id);
        setlist.Entries[2].Id.ShouldBe(first.Id);
        setlist.Entries[2].Note.ShouldBe("Again");
        setlist.Entries[1].Id.ShouldNotBe(first.Id);
        setlist.Entries.ShouldAllBe(entry => entry.SetlistId == setlist.Id);
        setlist.UpdatedAt.ShouldBe(T0.AddMinutes(2));
    }

    [Fact]
    public void ReplaceEntries_UnknownOrRepeatedIds_GetNewIds()
    {
        var setlist = NewSetlist();
        var foreign = Guid.NewGuid();

        setlist.ReplaceEntries([Entry(TrackA, id: foreign), Entry(TrackB, id: foreign)], Library, Guid.NewGuid, T0);
        var kept = setlist.Entries[0].Id;
        setlist.ReplaceEntries([Entry(TrackA, id: kept), Entry(TrackB, id: kept)], Library, Guid.NewGuid, T0);

        setlist.Entries[0].Id.ShouldBe(kept);
        setlist.Entries[1].Id.ShouldNotBe(kept);
    }

    [Fact]
    public void ReplaceEntries_WithNoEntries_EmptiesTheSetlist()
    {
        var setlist = NewSetlist();
        setlist.ReplaceEntries([Entry(TrackA)], Library, Guid.NewGuid, T0);

        setlist.ReplaceEntries([], Library, Guid.NewGuid, T0);

        setlist.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceEntries_FieldErrors_NameTheCodes()
    {
        var setlist = NewSetlist();

        var errors = Should.Throw<DomainValidationException>(() => setlist.ReplaceEntries(
            [Entry(Guid.NewGuid()), Entry(TrackA, MixOfB), Entry(TrackA, Guid.NewGuid()), Entry(TrackB, note: new string('n', 1001))], Library, Guid.NewGuid, T0)).Errors;

        errors.Select(error => error.Field).ShouldBe(["entries[0].trackId", "entries[1].mixId", "entries[2].mixId", "entries[3].note"]);
        errors[0].Message.ShouldContain(SetlistRules.TrackNotFound);
        errors[1].Message.ShouldContain(SetlistRules.MixTrackMismatch);
        setlist.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceEntries_MoreThan100_AreRefused()
    {
        var setlist = NewSetlist();
        var many = Enumerable.Range(0, 101).Select(_ => Entry(TrackA)).ToList();

        Should.Throw<DomainValidationException>(() => setlist.ReplaceEntries(many, Library, Guid.NewGuid, T0)).Errors.Single().Field.ShouldBe("entries");
        setlist.ReplaceEntries(many[..100], Library, Guid.NewGuid, T0);
        setlist.Entries.Count.ShouldBe(100);
    }

    [Fact]
    public void RemovedEntries_CountUp_AndAcknowledgeResets()
    {
        var setlist = NewSetlist();

        setlist.CountRemoved(2);
        setlist.CountRemoved(1);
        setlist.RemovedEntries.ShouldBe(3);

        setlist.AcknowledgeRemoved().ShouldBeTrue();
        setlist.RemovedEntries.ShouldBe(0);
        setlist.AcknowledgeRemoved().ShouldBeFalse();
        Should.Throw<ArgumentOutOfRangeException>(() => setlist.CountRemoved(-1));
    }
}
