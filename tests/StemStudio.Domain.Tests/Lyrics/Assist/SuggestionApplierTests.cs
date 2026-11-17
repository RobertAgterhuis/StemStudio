using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>023 T012: applying suggestions (data-model 4 "Accept"): descending order, overlap refusal, insert-after, set-title, limits.</summary>
public sealed class SuggestionApplierTests
{
    private const string Text = "the cat sat\non the mat";

    private static ApplicableSuggestion Replace(int line, int start, string original, string proposal, Guid? group = null) =>
        new(Guid.NewGuid(), new Anchor(line, start, original.Length, original), SuggestionOperation.Replace, proposal, group);

    [Fact]
    public void SeveralReplacementsOnOneLine_KeepOffsetsValid()
    {
        var first = Replace(1, 0, "the", "a");
        var second = Replace(1, 4, "cat", "dog");

        var outcome = SuggestionApplier.Apply("Song", Text, [first, second]);

        outcome.Succeeded.ShouldBeTrue();
        outcome.Text.ShouldBe("a dog sat\non the mat");
        outcome.Applied.ShouldBe([first.Id, second.Id]);
        outcome.AssistedLines.Select(l => l.LineText).ShouldBe(["a dog sat"]);
    }

    [Fact]
    public void InsertAfter_AddsLinesAndRecordsThem()
    {
        var insert = new ApplicableSuggestion(Guid.NewGuid(), new Anchor(1, 11, 0, string.Empty), SuggestionOperation.InsertAfter, "new one\nnew two");
        var replace = Replace(1, 4, "cat", "dog");

        var outcome = SuggestionApplier.Apply("Song", Text, [replace, insert]);

        outcome.Text.ShouldBe("the dog sat\nnew one\nnew two\non the mat");
        outcome.AssistedLines.Select(l => l.LineText).ShouldBe(["the dog sat", "new one", "new two"]);
    }

    [Fact]
    public void SetTitle_ChangesOnlyTheTitle()
    {
        var title = new ApplicableSuggestion(Guid.NewGuid(), new Anchor(0, 0, 4, "Song"), SuggestionOperation.SetTitle, "  Night Cat ");

        var outcome = SuggestionApplier.Apply("Song", Text, [title]);

        (outcome.Title, outcome.Text, outcome.AssistedLines.Count).ShouldBe(("Night Cat", Text, 0));
    }

    [Fact]
    public void InformationOnly_IsAppliedWithoutChange()
    {
        var note = new ApplicableSuggestion(Guid.NewGuid(), new Anchor(1, 0, 3, "the"), SuggestionOperation.None, null);

        SuggestionApplier.Apply("Song", Text, [note]).ShouldSatisfyAllConditions(
            o => o.Text.ShouldBe(Text), o => o.Applied.ShouldBe([note.Id]));
    }

    [Fact]
    public void OverlappingRanges_AreRefused()
    {
        var a = Replace(1, 0, "the cat", "a dog");
        var b = Replace(1, 4, "cat", "cow");
        var c = Replace(2, 0, "on", "at");

        var outcome = SuggestionApplier.Apply("Song", Text, [a, b, c]);

        outcome.Succeeded.ShouldBeFalse();
        outcome.Overlapping.ShouldBe([a.Id, b.Id]);
        outcome.Text.ShouldBe(Text);
    }

    [Fact]
    public void SameGroupTwoTitlesAndTwoInserts_Overlap()
    {
        var group = Guid.NewGuid();
        var alternatives = new[] { Replace(1, 0, "the cat sat", "x", group), Replace(2, 0, "on", "y", group) };
        var titles = Enumerable.Range(0, 2).Select(_ => new ApplicableSuggestion(Guid.NewGuid(), new Anchor(0, 0, 0, ""), SuggestionOperation.SetTitle, "T")).ToArray();
        var inserts = Enumerable.Range(0, 2).Select(_ => new ApplicableSuggestion(Guid.NewGuid(), new Anchor(2, 10, 0, ""), SuggestionOperation.InsertAfter, "z")).ToArray();

        SuggestionApplier.Apply("Song", Text, alternatives).Overlapping.Count.ShouldBe(2);
        SuggestionApplier.Apply("Song", Text, titles).Overlapping.Count.ShouldBe(2);
        SuggestionApplier.Apply("Song", Text, inserts).Overlapping.Count.ShouldBe(2);
        SuggestionApplier.Apply("Song", Text, [titles[0], inserts[0]]).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void StaleAnchors_AreSkipped()
    {
        var stale = Replace(1, 4, "cow", "dog");
        var outOfRange = Replace(7, 0, "x", "y");
        var good = Replace(2, 7, "mat", "rug");

        var outcome = SuggestionApplier.Apply("Song", Text, [stale, outOfRange, good]);

        outcome.Stale.ShouldBe([stale.Id, outOfRange.Id]);
        outcome.Text.ShouldBe("the cat sat\non the rug");
    }

    [Fact]
    public void ResultOver4096_IsTooLong()
    {
        var outcome = SuggestionApplier.Apply("Song", Text, [Replace(1, 0, "the", new string('x', 4096))]);

        (outcome.TooLong, outcome.Succeeded, outcome.Applied.Count).ShouldBe((true, false, 0));
    }

    [Fact]
    public void EmptyTitle_IsRefusedAsTooLong() =>
        SuggestionApplier.Apply("Song", Text, [new ApplicableSuggestion(Guid.NewGuid(), new Anchor(0, 0, 0, ""), SuggestionOperation.SetTitle, " ")]).TooLong.ShouldBeTrue();

    [Fact]
    public void Deletion_WithNullProposal()
    {
        var outcome = SuggestionApplier.Apply("Song", Text, [new ApplicableSuggestion(Guid.NewGuid(), new Anchor(1, 3, 4, " cat"), SuggestionOperation.Replace, null)]);

        outcome.Text.ShouldBe("the sat\non the mat");
    }
}
