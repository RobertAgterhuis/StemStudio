using System.Net;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.Components.InstrumentViews;
using StemStudio.Web.Experience;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>Chord sheet in the lyrics card and the chord editor (T070, FR-013, FR-015, FR-016, FR-062).</summary>
public sealed class ChordSheetTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;

    public ChordSheetTests()
    {
        Api.GetChordsAsync(Track, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords());
        Api.GetChordSheetAsync(Track, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Sheet());
    }

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, int capo = 0) =>
        RenderAtLevel<ChordSheet>(level, p => p.Add(c => c.TrackId, Track).Add(c => c.Capo, capo));

    [Fact]
    public void Rows_PutTheChordsAboveTheirWords_AsText()
    {
        var sheet = RenderAt(ExperienceLevel.Beginner);

        sheet.Find("[data-testid=chord-sheet]").GetAttribute("data-help-id").ShouldBe("chord-sheet");
        sheet.Find("[data-testid=chord-sheet-row-0]").TextContent.ShouldBe("AFake Eline one");
        sheet.Find("[data-testid=chord-sheet-chord-0-1]").TextContent.ShouldBe("E");
        sheet.Find("[data-testid=chord-sheet-chord-1-0]").TextContent.ShouldBe("F#m");
        sheet.Find("[data-testid=chord-sheet-capo]").TextContent.ShouldBe("Capo 2");
        sheet.FindAll("[data-testid=chord-sheet-download]").ShouldBeEmpty();
        sheet.FindAll("[data-testid^=chord-edit-]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Transpose_ReloadsTheSheet_Clamped()
    {
        var sheet = RenderAt(ExperienceLevel.Beginner, capo: 3);

        await sheet.Find("[data-testid=chord-sheet-transpose]").ChangeAsync(new() { Value = "20" });

        await Api.Received(1).GetChordSheetAsync(Track, 12, 3, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Advanced_HasTheChordProLink_WithTransposeAndCapo()
    {
        var link = RenderAt(ExperienceLevel.Advanced, capo: 2).Find("[data-testid=chord-sheet-download]");

        link.GetAttribute("href").ShouldEndWith($"/api/tracks/{Track}/chords/chordpro?transpose=0&capo=2");
        link.HasAttribute("download").ShouldBeTrue();
        link.GetAttribute("aria-label")!.ShouldContain("Download ChordPro");
    }

    [Fact]
    public void Advanced_HasAnEditButtonPerChord_ForItsSegment()
    {
        Api.GetChordSheetAsync(Track, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ChordSheetDto { Transpose = 0, Capo = 0, Rows = [new() { Text = "Line", Chords = [new() { CharIndex = 0, Symbol = "C", Lead = false }] }] });

        var edit = RenderAt(ExperienceLevel.Advanced).Find("[data-testid=chord-edit-1]");

        edit.GetAttribute("aria-label").ShouldBe("Edit chord C");
        edit.GetAttribute("data-help-id").ShouldBe("chord-edit");
    }

    [Fact]
    public async Task EditDialog_SavesTheHarteLabel_AndReloads()
    {
        Api.GetChordSheetAsync(Track, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ChordSheetDto { Transpose = 0, Capo = 0, Rows = [new() { Text = "Line", Chords = [new() { CharIndex = 0, Symbol = "C", Lead = false }] }] });
        var full = InstrumentViewsData.Chords() with { Document = InstrumentViewsData.Chords().Document! with { Vocabulary = ChordVocabularyDto.Full } };
        Api.GetChordsAsync(Track, Arg.Any<CancellationToken>()).Returns(full);
        Api.EditChordSegmentAsync(Track, 1, Arg.Any<EditChordRequest>(), Arg.Any<CancellationToken>()).Returns(full);
        var sheet = RenderAt(ExperienceLevel.Advanced);

        await sheet.Find("[data-testid=chord-edit-1]").ClickAsync(new());
        sheet.Find("[data-testid=chord-edit-dialog]").GetAttribute("role").ShouldBe("dialog");
        await sheet.Find("[data-testid=chord-edit-root]").ChangeAsync(new() { Value = "9" });
        await sheet.Find("[data-testid=chord-edit-quality]").ChangeAsync(new() { Value = "Min7" });
        await sheet.Find("[data-testid=chord-edit-bass]").ChangeAsync(new() { Value = "7" });
        await sheet.Find("[data-testid=chord-edit-save]").ClickAsync(new());

        await Api.Received(1).EditChordSegmentAsync(Track, 1, Arg.Is<EditChordRequest>(r => r.Label == "A:min7/b7"), Arg.Any<CancellationToken>());
        sheet.FindAll("[data-testid=chord-edit-dialog]").ShouldBeEmpty();
        await Api.Received(2).GetChordSheetAsync(Track, 0, 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditDialog_NoChord_SendsN_AndShowsErrors_AndCancels()
    {
        Api.GetChordSheetAsync(Track, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new ChordSheetDto { Transpose = 0, Capo = 0, Rows = [new() { Text = "Line", Chords = [new() { CharIndex = 0, Symbol = "C", Lead = false }] }] });
        Api.EditChordSegmentAsync(Track, 1, Arg.Any<EditChordRequest>(), Arg.Any<CancellationToken>())
            .Returns<TrackAnalysisDto<ChordChartDocumentDto>>(_ => throw new ApiException(new ApiError(HttpStatusCode.UnprocessableEntity, "Invalid", "The label is not a chord.", new Dictionary<string, string[]>())));
        var sheet = RenderAt(ExperienceLevel.Advanced);

        await sheet.Find("[data-testid=chord-edit-1]").ClickAsync(new());
        sheet.Find("[data-testid=chord-edit-quality]").HasAttribute("disabled").ShouldBeFalse();
        await sheet.Find("[data-testid=chord-edit-root]").ChangeAsync(new() { Value = "-1" });
        sheet.Find("[data-testid=chord-edit-quality]").HasAttribute("disabled").ShouldBeTrue();
        sheet.FindAll("[data-testid=chord-edit-quality] option").Count.ShouldBe(2);
        await sheet.Find("[data-testid=chord-edit-save]").ClickAsync(new());

        await Api.Received(1).EditChordSegmentAsync(Track, 1, Arg.Is<EditChordRequest>(r => r.Label == "N"), Arg.Any<CancellationToken>());
        sheet.Find("[data-testid=chord-edit-error]").TextContent.ShouldContain("not a chord");
        await sheet.Find("[data-testid=chord-edit-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        sheet.FindAll("[data-testid=chord-edit-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void ChordsNotReady_ShowsTheStatus_AndNoSheet()
    {
        Api.GetChordsAsync(Track, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords(AnalysisStatusDto.Running, 0.5), InstrumentViewsData.Chords());
        var sheet = RenderAt(ExperienceLevel.Beginner);

        sheet.Find("[data-testid=chord-sheet-empty]").TextContent.ShouldBe("Detecting chords: 50 %.");
        sheet.FindAll("[data-testid=chord-sheet]").ShouldBeEmpty();

        Timer.Tick();
        sheet.WaitForAssertion(() => sheet.Find("[data-testid=chord-sheet]"));
    }

    [Fact]
    public void NoChords_SaysSo()
    {
        Api.GetChordsAsync(Track, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.Chords() with { Document = InstrumentViewsData.Chords().Document! with { NoChords = true } });

        RenderAt(ExperienceLevel.Beginner).Find("[data-testid=chord-sheet-empty]").TextContent.ShouldBe("No chords were detected in this song.");
    }

    [Fact]
    public void SheetError_IsShown()
    {
        Api.GetChordSheetAsync(Track, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<ChordSheetDto>(_ => throw new ApiException(new ApiError(HttpStatusCode.Conflict, "Not ready", "Chords are not ready.", new Dictionary<string, string[]>())));

        RenderAt(ExperienceLevel.Beginner).Find("[role=alert]").TextContent.ShouldContain("Chords are not ready.");
    }

    [Fact]
    public void CapoChange_ReloadsTheSheet()
    {
        var sheet = Render<ChordSheet>(p => p.Add(c => c.TrackId, Track).Add(c => c.Capo, 0));

        sheet.Render(p => p.Add(c => c.Capo, 4));

        Api.Received(1).GetChordSheetAsync(Track, 0, 4, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Layout_SplitsTextAtTheChords_AndKeepsLeadChords()
    {
        var row = new ChordSheetRowDto
        {
            Text = "Hello world",
            Chords = [new() { CharIndex = 0, Symbol = "C", Lead = true }, new() { CharIndex = 0, Symbol = "G", Lead = false }, new() { CharIndex = 6, Symbol = "Am", Lead = false }],
        };

        ChordSheetLayout.Parts(row).Select(p => (p.Chord?.Symbol, p.Text)).ShouldBe([("C", ""), ("G", "Hello "), ("Am", "world")]);
        ChordSheetLayout.Parts(new ChordSheetRowDto { Text = "Intro", Chords = [new() { CharIndex = 2, Symbol = "D", Lead = false }] })
            .Select(p => (p.Chord?.Symbol, p.Text)).ShouldBe([(null, "In"), ("D", "tro")]);
        ChordSheetLayout.Parts(new ChordSheetRowDto { Text = "Spoken", Chords = [] }).Single().Text.ShouldBe("Spoken");
    }

    [Fact]
    public void Segments_MapByChordCount_OrNotAtAll()
    {
        var document = InstrumentViewsData.Chords().Document!;
        var one = new ChordSheetDto { Transpose = 0, Capo = 0, Rows = [new() { Text = "", Chords = [new() { CharIndex = 0, Symbol = "C", Lead = false }] }] };
        var two = one with { Rows = [one.Rows[0] with { Chords = [.. one.Rows[0].Chords, .. one.Rows[0].Chords] }] };

        ChordSheetSegments.Build(one, document)!.ForChord(0, 0).ShouldBe(1);
        ChordSheetSegments.Build(two, document)!.ForChord(0, 1).ShouldBe(1);
        ChordSheetSegments.Build(InstrumentViewsData.Sheet(), document).ShouldBeNull();
        ChordSheetSegments.Build(one, document)!.ForChord(3, 0).ShouldBeNull();
    }
}
