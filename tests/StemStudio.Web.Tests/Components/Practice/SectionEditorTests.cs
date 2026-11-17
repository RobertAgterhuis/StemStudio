using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Sections;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Components.Practice;

/// <summary>T072: the section editor in the mixer (FR-006, FR-007; ui-test-ids 1.13.0 section-editor-*, section-overwrite-*).</summary>
public sealed class SectionEditorTests : MixerTestContext
{
    public SectionEditorTests() =>
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet() with { Snap = SectionSnapDto.Time });

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level)
    {
        var page = Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));
        page.WaitForAssertion(() => page.FindAll("[data-testid^=section-0b1c]").Count.ShouldBe(5));
        return page;
    }

    private static Task Click(IRenderedComponent<CascadingValue<ExperienceContext>> page, string testId) =>
        page.InvokeAsync(() => page.Find($"[data-testid={testId}]").Click());

    private async Task<IRenderedComponent<CascadingValue<ExperienceContext>>> OpenEditorAsync(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var page = RenderAt(level);
        await Click(page, "section-edit");
        return page;
    }

    private static string[] Labels(IRenderedComponent<CascadingValue<ExperienceContext>> page) =>
        [.. page.FindAll("[data-testid^=section-editor-label-]").Select(e => e.GetAttribute("value")!)];

    [Fact]
    public void Beginner_HasNoEditButton_ExpertHasDetectAgainAndConfidence()
    {
        var beginner = RenderAt(ExperienceLevel.Beginner);
        beginner.FindAll("[data-testid=section-edit]").ShouldBeEmpty();
        beginner.FindAll("[data-testid^=section-confidence-]").ShouldBeEmpty();

        var advanced = RenderAt(ExperienceLevel.Advanced);
        advanced.FindAll("[data-testid=section-detect-again]").ShouldBeEmpty();

        var expert = RenderAt(ExperienceLevel.Expert);
        expert.Find($"[data-testid=section-confidence-{PracticeFlowData.SectionIds[0]}]").TextContent.ShouldBe("80 %");
        expert.Find("[data-testid=section-detect-again]").ShouldNotBeNull();
    }

    [Fact]
    public async Task Editor_ListsEveryRow_WithItsFields()
    {
        var page = await OpenEditorAsync();

        page.FindAll("[data-testid^=section-editor-row-]").Count.ShouldBe(5);
        Labels(page).ShouldBe(["Intro", "Verse 1", "Chorus 1", "Guitar solo", "Outro"]);
        page.Find("[data-testid=section-editor-kind-4]").GetAttribute("value").ShouldBe("solo");
        page.Find("[data-testid=section-editor-instrument-4]").GetAttribute("value").ShouldBe("guitar");
        page.FindAll("[data-testid=section-editor-instrument-1]").ShouldBeEmpty();
        page.Find("[data-testid=section-editor-start-2]").GetAttribute("value").ShouldBe("8.1");
        page.FindAll("[data-testid=section-editor-merge-5]").ShouldBeEmpty();
        page.Find("[data-testid=section-edit]").GetAttribute("aria-expanded").ShouldBe("true");
    }

    [Fact]
    public async Task SplitRenameKindAndBoundary_ThenDone_SavesTheWholeListWithTheRevision()
    {
        Api.ReplaceSectionsAsync(TestData.TrackId, Arg.Any<SectionsUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PracticeFlowData.SectionSet(revision: 2) with { Origin = SectionSetOriginDto.Edited });
        var page = await OpenEditorAsync();
        Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, 15.3);
        page.WaitForAssertion(() => page.Find($"[data-testid=section-{PracticeFlowData.SectionIds[1]}]").GetAttribute("data-current").ShouldBe("true"));

        await Click(page, "section-editor-split");
        await page.InvokeAsync(() => page.Find("[data-testid=section-editor-label-1]").Change("Count-off"));
        await page.InvokeAsync(() => page.Find("[data-testid=section-editor-kind-6]").Change("bridge"));
        await page.InvokeAsync(() => page.Find("[data-testid=section-editor-start-3]").Change("16.2"));
        await page.InvokeAsync(() => page.Find("[data-testid=section-editor-start-3]").Change("x"));
        Labels(page).ShouldBe(["Count-off", "Verse 1", "Verse 2", "Chorus 1", "Guitar solo", "Bridge 1"]);
        await Click(page, "section-editor-done");

        await Api.Received(1).ReplaceSectionsAsync(TestData.TrackId, Arg.Is<SectionsUpdateRequest>(r =>
            r.Revision == 1 && r.Sections.Count == 6 && r.Sections[2].StartSeconds == 16.2 && r.Sections[1].EndSeconds == 16.2
            && r.Sections[0].Label == "Count-off" && r.Sections[5].Kind == SectionKindDto.Bridge), Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=section-editor]").ShouldBeEmpty();
        page.Find("[data-testid=section-lane]").GetAttribute("data-revision").ShouldBe("2");
    }

    [Fact]
    public async Task MergeDeleteAndAdd_ChangeTheRows()
    {
        var page = await OpenEditorAsync();

        await Click(page, "section-editor-merge-1");
        Labels(page).ShouldBe(["Intro", "Chorus 1", "Guitar solo", "Outro"]);
        await Click(page, "section-editor-delete-4");
        page.FindAll("[data-testid^=section-editor-row-]").Count.ShouldBe(3);
        await Click(page, "section-editor-add");
        Labels(page)[^1].ShouldBe("Other");
        await page.InvokeAsync(() => page.Find("[data-testid=section-editor-kind-3]").Change("solo"));
        await page.InvokeAsync(() => page.Find("[data-testid=section-editor-instrument-3]").Change("bass"));
        Labels(page)[2].ShouldBe("Bass solo");
    }

    [Fact]
    public async Task SplitOutsideASection_SaysWhy()
    {
        var page = await OpenEditorAsync();
        Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, 8.3);
        page.WaitForAssertion(() => page.Find($"[data-testid=section-{PracticeFlowData.SectionIds[1]}]").GetAttribute("data-current").ShouldBe("true"));

        await Click(page, "section-editor-split");

        page.Find("[data-testid=section-editor-error]").TextContent.ShouldContain("at least one second");
    }

    [Fact]
    public async Task Cancel_DiscardsTheDraft()
    {
        var page = await OpenEditorAsync();
        await Click(page, "section-editor-merge-1");

        await Click(page, "section-editor-cancel");
        await Click(page, "section-edit");

        Labels(page).Length.ShouldBe(5);
        await Api.DidNotReceive().ReplaceSectionsAsync(Arg.Any<Guid>(), Arg.Any<SectionsUpdateRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, "sections-changed", "Another window saved the sections first. Cancel and open the editor again to see them.")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "validation", "Label is too long.")]
    public async Task SaveErrors_AreShownInTheEditor(HttpStatusCode status, string code, string message)
    {
        Api.ReplaceSectionsAsync(TestData.TrackId, Arg.Any<SectionsUpdateRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiException(new ApiError(status, "Problem", "Label is too long.", new Dictionary<string, string[]>())
            {
                Type = $"https://stemstudio.local/problems/{code}",
            }));
        var page = await OpenEditorAsync();

        await Click(page, "section-editor-done");

        page.Find("[data-testid=section-editor-error]").TextContent.ShouldBe(message);
        page.Find("[data-testid=section-editor]").ShouldNotBeNull();
    }

    [Fact]
    public async Task AnInvalidDraft_IsNotSent()
    {
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet(sections:
            [PracticeFlowData.Section(0, SectionKindDto.Intro, "Intro", (0, 0.5))]));
        var page = Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(ExperienceLevel.Advanced, DensityPreference.Comfortable))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));
        page.WaitForAssertion(() => page.FindAll("[data-testid^=section-0b1c]").Count.ShouldBe(1));
        await Click(page, "section-edit");

        await Click(page, "section-editor-done");

        page.Find("[data-testid=section-editor-error]").TextContent.ShouldBe("\"Intro\" is shorter than one second.");
        await Api.DidNotReceive().ReplaceSectionsAsync(Arg.Any<Guid>(), Arg.Any<SectionsUpdateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetectAgain_OnDetectedSections_DetectsAtOnce()
    {
        Api.DetectSectionsAsync(TestData.TrackId, false, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet(SectionSetStatusDto.Queued));
        var page = RenderAt(ExperienceLevel.Expert);

        await Click(page, "section-detect-again");

        page.FindAll("[data-testid=section-overwrite-confirm]").ShouldBeEmpty();
        page.Find("[data-testid=section-lane]").GetAttribute("data-status").ShouldBe("queued");
    }

    [Fact]
    public async Task DetectAgain_OnEditedSections_AsksFirst()
    {
        Api.GetSectionsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet() with { Origin = SectionSetOriginDto.Edited });
        Api.DetectSectionsAsync(TestData.TrackId, true, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.SectionSet(SectionSetStatusDto.Queued));
        var page = RenderAt(ExperienceLevel.Expert);

        await Click(page, "section-detect-again");
        await Click(page, "section-overwrite-cancel");
        await Api.DidNotReceive().DetectSectionsAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await Click(page, "section-detect-again");
        await Click(page, "section-overwrite-confirm");

        await Api.Received(1).DetectSectionsAsync(TestData.TrackId, true, Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=section-overwrite-confirm]").ShouldBeEmpty();
    }

    [Fact]
    public async Task DetectAgain_Refused_ShowsAToast()
    {
        Api.DetectSectionsAsync(TestData.TrackId, false, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiException(new ApiError(HttpStatusCode.ServiceUnavailable, "Down", "The worker is busy.", new Dictionary<string, string[]>())));
        var page = RenderAt(ExperienceLevel.Expert);

        await Click(page, "section-detect-again");

        Services.GetRequiredService<StemStudio.Web.Services.ToastService>().Message.ShouldBe("The worker is busy.");
    }
}
