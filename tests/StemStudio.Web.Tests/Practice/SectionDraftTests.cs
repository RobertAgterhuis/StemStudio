using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Practice;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Practice;

/// <summary>T072: the section editor's working copy (FR-006): split, merge, rename, kind, boundaries, delete, add, renumbering, validation.</summary>
public sealed class SectionDraftTests
{
    private static readonly BeatGridDto Grid = new(120, [.. Enumerable.Range(0, 130).Select(i => i * 0.5)], [.. Enumerable.Range(0, 33).Select(i => i * 2.0)], 4, 0.9, true);

    private static SectionDraft Draft(SectionSnapDto snap = SectionSnapDto.Downbeat) =>
        new(PracticeFlowData.SectionSet() with { Snap = snap }, 64, Grid);

    private static string[] Labels(SectionDraft draft) => [.. draft.Rows.Select(r => r.Label)];

    [Fact]
    public void Split_AtThePlayhead_SnapsToTheDownbeat_AndRenumbers()
    {
        var draft = Draft();

        draft.Split(15.3).ShouldBeTrue();

        draft.Rows[1].End.ShouldBe(16);
        (draft.Rows[2].Start, draft.Rows[2].End, draft.Rows[2].Kind, draft.Rows[2].Id).ShouldBe((16, 24.1, SectionKindDto.Verse, (Guid?)null));
        Labels(draft).ShouldBe(["Intro", "Verse 1", "Verse 2", "Chorus 1", "Guitar solo", "Outro"]);
    }

    [Theory]
    [InlineData(SectionSnapDto.Beat, 15.3, 15.5)]
    [InlineData(SectionSnapDto.Time, 15.34, 15.3)]
    public void Split_SnapsToBeatsOrTenths(SectionSnapDto snap, double position, double expected)
    {
        var draft = Draft(snap);

        draft.Split(position);

        draft.Rows[2].Start.ShouldBe(expected);
    }

    [Fact]
    public void Split_IsRefusedWithinOneSecondOfABoundary()
    {
        var draft = Draft(SectionSnapDto.Time);

        draft.Split(8.6).ShouldBeFalse();
        draft.Split(23.5).ShouldBeFalse();
        draft.Rows.Count.ShouldBe(5);
    }

    [Fact]
    public void Merge_KeepsTheFirstIdKindAndLabel()
    {
        var draft = Draft();

        draft.Merge(1);
        draft.Merge(9);

        draft.Rows.Count.ShouldBe(4);
        (draft.Rows[1].Id, draft.Rows[1].Kind, draft.Rows[1].Label, draft.Rows[1].End).ShouldBe((PracticeFlowData.SectionIds[1], SectionKindDto.Verse, "Verse 1", 40.1));
    }

    [Fact]
    public void Rename_KeepsTheName_ThroughRenumbering_AndEmptyGivesTheAutoLabelBack()
    {
        var draft = Draft();
        draft.Rename(1, "  First verse  ");
        draft.Split(15.3);

        Labels(draft).ShouldBe(["Intro", "First verse", "Verse 1", "Chorus 1", "Guitar solo", "Outro"]);
        draft.Rename(1, " ");
        Labels(draft)[1..3].ShouldBe(["Verse 1", "Verse 2"]);
        draft.Rename(0, new string('x', 50));
        draft.Rows[0].Label.Length.ShouldBe(40);
    }

    [Fact]
    public void Kind_InstrumentOnlyForSolos_AndSoloLabelsNameTheInstrument()
    {
        var draft = Draft();

        draft.SetKind(3, SectionKindDto.Bridge);
        draft.Rows[3].Instrument.ShouldBeNull();
        draft.Rows[3].Label.ShouldBe("Bridge 1");
        draft.SetInstrument(3, "bass");
        draft.Rows[3].Instrument.ShouldBeNull();
        draft.SetKind(3, SectionKindDto.Solo);
        draft.SetInstrument(3, "drums.snare");
        draft.Rows[3].Label.ShouldBe("Drums snare solo");
        draft.SetInstrument(3, string.Empty);
        draft.Rows[3].Label.ShouldBe("Solo");
        draft.SetKind(0, SectionKindDto.PreChorus);
        draft.Rows[0].Label.ShouldBe("Pre-chorus 1");
    }

    [Fact]
    public void MoveStart_MovesTheSharedBoundary_KeepingOneSecondOnBothSides()
    {
        var draft = Draft();

        draft.MoveStart(1, 10.04).ShouldBe(10);
        draft.Rows[0].End.ShouldBe(10);
        draft.MoveStart(1, 0.2).ShouldBe(1);
        draft.MoveStart(1, 30).ShouldBe(23.1);
        draft.MoveStart(0, 5).ShouldBe(5);
    }

    [Fact]
    public void Delete_GivesTheTimeToTheNeighbour_AndAddFillsTheRest()
    {
        var draft = new SectionDraft(PracticeFlowData.SectionSet(), 70, null);

        draft.Delete(1);
        draft.Rows[0].End.ShouldBe(24.1);
        draft.Delete(0);
        draft.Rows[0].Start.ShouldBe(0);
        draft.Delete(-1);
        draft.CanAdd.ShouldBeTrue();
        draft.Add();
        (draft.Rows[^1].Start, draft.Rows[^1].End, draft.Rows[^1].Label).ShouldBe((64, 70, "Other"));
        draft.CanAdd.ShouldBeFalse();
        draft.Add();
        draft.Rows.Count.ShouldBe(4);
    }

    [Fact]
    public void Delete_WithAGap_LeavesTheNeighboursAlone()
    {
        var set = PracticeFlowData.SectionSet(sections:
            [PracticeFlowData.Section(0, SectionKindDto.Intro, "Intro", (0, 5)), PracticeFlowData.Section(1, SectionKindDto.Verse, "Verse 1", (8, 20)),
             PracticeFlowData.Section(2, SectionKindDto.Outro, "Outro", (25, 30))]);
        var draft = new SectionDraft(set, 30, null);

        draft.Delete(1);

        (draft.Rows[0].End, draft.Rows[1].Start).ShouldBe((5, 25));
        draft.MoveStart(1, 2).ShouldBe(5);
    }

    [Fact]
    public void AnEmptySongCanStartFromNothing()
    {
        var draft = new SectionDraft(PracticeFlowData.SectionSet(sections: []), 30, null);

        draft.Problem().ShouldBe("Keep between 1 and 64 sections.");
        draft.Add();
        (draft.Rows.Single().Start, draft.Rows.Single().End).ShouldBe((0, 30));
        draft.Problem().ShouldBeNull();
    }

    [Fact]
    public void ToRequest_CarriesTheRevisionIdsAndSoloInstrument()
    {
        var draft = Draft();
        draft.Split(15.3);

        var request = draft.ToRequest();

        request.Revision.ShouldBe(1);
        request.Sections.Count.ShouldBe(6);
        request.Sections[2].Id.ShouldBeNull();
        request.Sections[4].Instrument.ShouldBe("guitar");
        request.Sections[0].Instrument.ShouldBeNull();
    }

    [Fact]
    public void AutoLabels_AreRecognised()
    {
        SectionDraft.IsAutoLabel("Verse 3", SectionKindDto.Verse, null).ShouldBeTrue();
        SectionDraft.IsAutoLabel("Verse x", SectionKindDto.Verse, null).ShouldBeFalse();
        SectionDraft.IsAutoLabel("Guitar solo", SectionKindDto.Solo, "guitar").ShouldBeTrue();
        SectionDraft.IsAutoLabel("Big finish", SectionKindDto.Outro, null).ShouldBeFalse();
        Should.Throw<ArgumentNullException>(() => SectionDraft.IsAutoLabel(null!, SectionKindDto.Other, null));
        Should.Throw<ArgumentNullException>(() => new SectionDraft(null!, 1, null));
    }
}
