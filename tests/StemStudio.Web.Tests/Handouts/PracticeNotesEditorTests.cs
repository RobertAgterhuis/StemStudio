using System.Net;
using Bunit;
using NSubstitute;
using StemStudio.Contracts;
using StemStudio.Contracts.Handouts;
using StemStudio.Web.Components.Handouts;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.InstrumentViews;

namespace StemStudio.Web.Tests.Handouts;

/// <summary>Practice notes per family (T076, FR-015; ui-test-ids practice-notes-*): family selector, counter, save, live status.</summary>
public sealed class PracticeNotesEditorTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;

    public PracticeNotesEditorTests()
    {
        Api.GetPracticeNotesAsync(Track, Arg.Any<CancellationToken>()).Returns(new PracticeNotesDto
        {
            Notes = [InsightsData.Note(HandoutFamilyDto.Bass, "Walk the bass line."), InsightsData.Note(HandoutFamilyDto.Drums, "Ghost notes.")],
        });
        Api.SetPracticeNoteAsync(Track, Arg.Any<HandoutFamilyDto>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => InsightsData.Note(call.ArgAt<HandoutFamilyDto>(1), call.ArgAt<string>(2)));
    }

    private IRenderedComponent<PracticeNotesEditor> RenderEditor() => Render<PracticeNotesEditor>(p => p.Add(c => c.TrackId, Track));

    [Fact]
    public void Region_IsLabelled_WithTheFamiliesAndTheFirstFamilysNote()
    {
        var editor = RenderEditor();

        var region = editor.Find("[data-testid=practice-notes]");
        region.LocalName.ShouldBe("section");
        editor.Find($"#{region.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Practice notes");
        editor.FindAll("[data-testid=practice-notes-family] option").Select(o => o.TextContent).ShouldBe(
            ["Band overview", "Vocals", "Guitar", "Bass", "Keys", "Drums", "Melodic instruments"]);
        editor.Find("[data-testid=practice-notes-text]").GetAttribute("value").ShouldBe(string.Empty);
        editor.Find("[data-testid=practice-notes-text]").GetAttribute("maxlength").ShouldBe("4000");
        editor.Find("[data-testid=practice-notes-saved]").GetAttribute("aria-live").ShouldBe("polite");
    }

    [Fact]
    public void ChoosingAFamily_ShowsItsNote_AndTheCounter()
    {
        var editor = RenderEditor();

        editor.Find("[data-testid=practice-notes-family]").Change("bass");

        editor.Find("[data-testid=practice-notes-text]").GetAttribute("value").ShouldBe("Walk the bass line.");
        editor.Find("[data-testid=practice-notes-count]").TextContent.ShouldBe("19 / 4,000 characters");
        editor.Find("[data-testid=practice-notes-text]").GetAttribute("aria-describedby").ShouldBe("practice-notes-count");
    }

    [Fact]
    public async Task Save_PutsTheNote_AndAnnouncesIt()
    {
        var editor = RenderEditor();
        editor.Find("[data-testid=practice-notes-family]").Change("melodic");
        editor.Find("[data-testid=practice-notes-text]").Input("Long tones in B-flat.");

        await editor.Find("[data-testid=practice-notes-save]").ClickAsync(new());

        await Api.Received(1).SetPracticeNoteAsync(Track, HandoutFamilyDto.Melodic, "Long tones in B-flat.", Arg.Any<CancellationToken>());
        editor.Find("[data-testid=practice-notes-saved]").TextContent.ShouldBe("Saved the note for Melodic instruments.");
    }

    [Fact]
    public async Task EmptyText_RemovesTheNote()
    {
        Api.SetPracticeNoteAsync(Track, HandoutFamilyDto.Bass, string.Empty, Arg.Any<CancellationToken>()).Returns((PracticeNoteDto?)null);
        var editor = RenderEditor();
        editor.Find("[data-testid=practice-notes-family]").Change("bass");
        editor.Find("[data-testid=practice-notes-text]").Input(string.Empty);

        await editor.Find("[data-testid=practice-notes-save]").ClickAsync(new());

        editor.Find("[data-testid=practice-notes-saved]").TextContent.ShouldBe("Removed the note for Bass.");
        editor.Find("[data-testid=practice-notes-family]").Change("drums");
        editor.Find("[data-testid=practice-notes-family]").Change("bass");
        editor.Find("[data-testid=practice-notes-text]").GetAttribute("value").ShouldBe(string.Empty);
    }

    [Fact]
    public async Task SaveError_IsAnnounced_AndTheTextIsKept()
    {
        Api.SetPracticeNoteAsync(Track, Arg.Any<HandoutFamilyDto>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<PracticeNoteDto?>(_ => throw Problem(HttpStatusCode.UnprocessableEntity, "The note is too long."));
        var editor = RenderEditor();
        editor.Find("[data-testid=practice-notes-text]").Input("Too long");

        await editor.Find("[data-testid=practice-notes-save]").ClickAsync(new());

        editor.Find("[data-testid=practice-notes-saved]").TextContent.ShouldContain("too long");
        editor.Find("[data-testid=practice-notes-text]").GetAttribute("value").ShouldBe("Too long");
    }

    [Fact]
    public void LoadError_IsShown()
    {
        Api.GetPracticeNotesAsync(Track, Arg.Any<CancellationToken>()).Returns<PracticeNotesDto>(_ => throw Problem(HttpStatusCode.NotFound, "Track not found"));

        RenderEditor().Find("[data-testid=practice-notes-saved]").TextContent.ShouldContain("Track not found");
    }

    [Fact]
    public async Task Focus_SelectsTheFamily_ForTheWriteNotesLink()
    {
        var editor = RenderEditor();

        await editor.InvokeAsync(() => editor.Instance.FocusAsync(HandoutFamilyDto.Drums));

        editor.Find("[data-testid=practice-notes-family]").GetAttribute("value").ShouldBe("drums");
        editor.Find("[data-testid=practice-notes-text]").GetAttribute("value").ShouldBe("Ghost notes.");
    }

    [Fact]
    public void Accessibility_ControlsAreLabelled_AndTheLimitIsTheContractOne()
    {
        var editor = RenderEditor();

        editor.Find("label[for=practice-notes-family]").TextContent.ShouldBe("Family");
        editor.Find("label[for=practice-notes-text]").TextContent.ShouldBe("Note");
        editor.Find("[data-testid=practice-notes-count]").TextContent.ShouldContain(ContractLimits.MaxPracticeNoteLength.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(editor.Find("[data-testid=practice-notes]"));
    }
}
