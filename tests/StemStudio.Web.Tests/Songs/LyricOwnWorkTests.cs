using Bunit;
using NSubstitute;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>023 T076: the own-work override in the editor (US6, FR-031, FR-028).</summary>
public sealed class LyricOwnWorkTests : LyricEditorTestContext
{
    private static readonly OwnWorkOverrideDto Confirmed = new()
    {
        StatementVersion = "own-work.v1",
        ConfirmedBy = "Robin",
        ConfirmedAt = new DateTimeOffset(2026, 11, 17, 9, 30, 0, TimeSpan.Zero),
        MatchedTrackId = TrackId,
        MatchedTrackTitle = "Fake Song",
    };

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> Refused(bool overridable = true)
    {
        Api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>()).Returns<Task<LyricAssistDto>>(_ => throw NotOwn(overridable));
        var page = Open(ExperienceLevel.Beginner);
        page.Find(Id("lyric-assist-check")).Click();
        page.WaitForElement(Id("lyric-assist-not-own"));
        return page;
    }

    [Fact]
    public void NotOwn_NamesTheTrack_AndOffersTheConfirmation()
    {
        var page = Refused();

        page.Find(Id("lyric-assist-not-own")).TextContent.ShouldContain("This text matches the lyrics of Fake Song. The assistant works on your own lyrics only.");
        page.Find(Id("lyric-assist-not-own")).GetAttribute("role").ShouldBe("alert");
        page.FindAll(Id("lyric-assist-own-work-open")).Count.ShouldBe(1);
    }

    [Fact]
    public void NotOverridable_OffersNoConfirmation()
    {
        var page = Refused(overridable: false);

        page.FindAll(Id("lyric-assist-own-work-open")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Dialog_ShowsTheStatement_AndConfirmNeedsNameAndTick()
    {
        var page = Refused();

        await page.Find(Id("lyric-assist-own-work-open")).ClickAsync(new());

        var dialog = page.Find(Id("lyric-own-work-dialog"));
        (dialog.GetAttribute("role"), dialog.GetAttribute("aria-modal")).ShouldBe(("dialog", "true"));
        var statement = page.Find(Id("lyric-own-work-statement"));
        (statement.TextContent, statement.GetAttribute("data-version")).ShouldBe(("I confirm that I wrote this text myself.", "own-work.v1"));
        page.Find(Id("lyric-own-work-track")).TextContent.ShouldBe("Fake Song");
        page.Find(Id("lyric-own-work-name")).Closest("label")!.TextContent.ShouldContain("Your name");
        page.Find(Id("lyric-own-work-check")).Closest("label")!.TextContent.ShouldContain("I confirm the statement above");
        Confirm().HasAttribute("disabled").ShouldBeTrue();
        await page.Find(Id("lyric-own-work-name")).InputAsync(new() { Value = "Robin" });
        Confirm().HasAttribute("disabled").ShouldBeTrue();
        await page.Find(Id("lyric-own-work-check")).ChangeAsync(new() { Value = true });
        Confirm().HasAttribute("disabled").ShouldBeFalse();
        await page.Find(Id("lyric-own-work-name")).InputAsync(new() { Value = "  " });
        Confirm().HasAttribute("disabled").ShouldBeTrue();

        AngleSharp.Dom.IElement Confirm() => page.Find(Id("lyric-own-work-confirm"));
    }

    [Fact]
    public async Task Cancel_SendsNothing()
    {
        var page = Refused();
        await page.Find(Id("lyric-assist-own-work-open")).ClickAsync(new());

        await page.Find(Id("lyric-own-work-cancel")).ClickAsync(new());

        page.FindAll(Id("lyric-own-work-dialog")).ShouldBeEmpty();
        await Api.DidNotReceive().ConfirmOwnWorkAsync(Arg.Any<Guid>(), Arg.Any<OwnWorkConfirmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Escape_ClosesTheDialog()
    {
        var page = Refused();
        await page.Find(Id("lyric-assist-own-work-open")).ClickAsync(new());

        await page.Find(Id("lyric-own-work-dialog")).KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        page.FindAll(Id("lyric-own-work-dialog")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Confirm_StoresTheOverride_AndShowsTheBadge()
    {
        Api.ConfirmOwnWorkAsync(LyricsId, Arg.Any<OwnWorkConfirmRequest>(), Arg.Any<CancellationToken>()).Returns(Confirmed);
        var page = Refused();
        await page.Find(Id("lyric-assist-own-work-open")).ClickAsync(new());
        await page.Find(Id("lyric-own-work-name")).InputAsync(new() { Value = "Robin" });
        await page.Find(Id("lyric-own-work-check")).ChangeAsync(new() { Value = true });

        await page.Find(Id("lyric-own-work-confirm")).ClickAsync(new());

        await Api.Received(1).ConfirmOwnWorkAsync(LyricsId, Arg.Is<OwnWorkConfirmRequest>(r =>
            r.MatchedTrackId == TrackId && r.ConfirmedBy == "Robin" && r.StatementVersion == "own-work.v1" && r.Confirm), Arg.Any<CancellationToken>());
        page.FindAll(Id("lyric-own-work-dialog")).ShouldBeEmpty();
        page.FindAll(Id("lyric-assist-not-own")).ShouldBeEmpty();
        var badge = page.Find(Id("lyric-own-work-badge"));
        badge.TextContent.ShouldContain("Own work confirmed by Robin on 17 November 2026 (matches Fake Song)");
        (badge.GetAttribute("data-track-id"), badge.GetAttribute("data-confirmed-at")).ShouldBe((TrackId.ToString(), "2026-11-17T09:30:00.0000000+00:00"));
    }

    [Fact]
    public async Task Revoke_AsksFirst_ThenTheRefusalComesBack()
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false, true);
        Api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>()).Returns<Task<LyricAssistDto>>(_ => throw NotOwn());
        var page = Open(ExperienceLevel.Beginner, Sheet(assist: SheetAssistInfoDto.Default with { OwnWorkOverride = Confirmed }));
        page.Find(Id("lyric-own-work-revoke")).TextContent.Trim().ShouldBe("Revoke");

        await page.Find(Id("lyric-own-work-revoke")).ClickAsync(new());
        await Api.DidNotReceive().RevokeOwnWorkAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await page.Find(Id("lyric-own-work-revoke")).ClickAsync(new());

        await Api.Received(1).RevokeOwnWorkAsync(LyricsId, Arg.Any<CancellationToken>());
        page.FindAll(Id("lyric-own-work-badge")).ShouldBeEmpty();
        await page.Find(Id("lyric-assist-check")).ClickAsync(new());
        page.WaitForElement(Id("lyric-assist-not-own"));
    }

    [Fact]
    public async Task ConfirmRejected_KeepsTheDialogAndShowsTheError()
    {
        Api.ConfirmOwnWorkAsync(LyricsId, Arg.Any<OwnWorkConfirmRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<OwnWorkOverrideDto>>(_ => throw LyricAssistData.Problem(LyricAssistProblemTypes.OverrideNotApplicable));
        var page = Refused();
        await page.Find(Id("lyric-assist-own-work-open")).ClickAsync(new());
        await page.Find(Id("lyric-own-work-name")).InputAsync(new() { Value = "Robin" });
        await page.Find(Id("lyric-own-work-check")).ChangeAsync(new() { Value = true });

        await page.Find(Id("lyric-own-work-confirm")).ClickAsync(new());

        page.FindAll(Id("lyric-own-work-dialog")).Count.ShouldBe(1);
        page.Find(Id("lyric-assist-error")).TextContent.ShouldBe("Problem override-not-applicable");
    }
}
