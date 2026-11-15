using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Artwork;

/// <summary>Own picture on the track page: upload, client checks, progress, errors, reset; Advanced+ only (005 T048, FR-013, FR-015).</summary>
public sealed class ArtworkActionsTests : PageTestContext
{
    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, TrackDto track)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(track);
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<TrackDetail>(d => d.Add(x => x.Id, TestData.TrackId)));
    }

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAdvanced(TrackDto? track = null) =>
        RenderAt(ExperienceLevel.Advanced, track ?? ArtworkData.WithArtwork(ArtworkSourceDto.Embedded));

    private static void Choose(IRenderedComponent<CascadingValue<ExperienceContext>> page, InputFileContent file) =>
        page.FindComponent<InputFile>().UploadFiles(file);

    private static InputFileContent Png(int bytes = 2048, string type = "image/png", string name = "own.png") =>
        InputFileContent.CreateFromBinary(new byte[bytes], name, null, type);

    [Theory]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public void AdvancedAndExpert_HaveTheUploadInput(ExperienceLevel level)
    {
        var input = RenderAt(level, ArtworkData.WithArtwork()).Find("[data-testid=artwork-upload-input]");

        input.GetAttribute("accept").ShouldBe("image/jpeg,image/png,image/webp");
        input.GetAttribute("type").ShouldBe("file");
    }

    [Fact]
    public void Beginner_HasNoPictureActions()
    {
        var page = RenderAt(ExperienceLevel.Beginner, ArtworkData.WithArtwork(ArtworkSourceDto.User));

        page.FindAll("[data-testid=artwork-upload-input]").ShouldBeEmpty();
        page.FindAll("[data-testid=artwork-reset]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ArtworkSourceDto.User, true)]
    [InlineData(ArtworkSourceDto.Embedded, false)]
    [InlineData(ArtworkSourceDto.Spotify, false)]
    public void Reset_ExistsOnlyForAnOwnPicture(ArtworkSourceDto source, bool shown) =>
        (RenderAdvanced(ArtworkData.WithArtwork(source)).FindAll("[data-testid=artwork-reset]").Count == 1).ShouldBe(shown);

    [Fact]
    public void NoPicture_HasNoReset() => RenderAdvanced(TestData.Track()).FindAll("[data-testid=artwork-reset]").ShouldBeEmpty();

    [Fact]
    public void Upload_StreamsThePicture_AndShowsTheNewCover()
    {
        Api.SetArtworkAsync(TestData.TrackId, Arg.Any<UploadFile>(), Arg.Any<CancellationToken>()).Returns(ArtworkData.WithArtwork(ArtworkSourceDto.User));
        var page = RenderAdvanced();

        Choose(page, Png());

        page.WaitForAssertion(() => page.Find("[data-testid=track-cover]").GetAttribute("data-artwork-source").ShouldBe("user"));
        Api.Received(1).SetArtworkAsync(TestData.TrackId, Arg.Is<UploadFile>(f => f.FileName == "own.png" && f.ContentType == "image/png"), Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=artwork-error]").ShouldBeEmpty();
        page.FindAll("[data-testid=artwork-reset]").Count.ShouldBe(1);
    }

    [Fact]
    public void WhileUploading_ShowsProgressAndDisablesTheInput()
    {
        var pending = new TaskCompletionSource<TrackDto>();
        Api.SetArtworkAsync(TestData.TrackId, Arg.Any<UploadFile>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var page = RenderAdvanced();

        Choose(page, Png());

        page.Find("[data-testid=artwork-progress]").TextContent.ShouldBe("Uploading picture...");
        page.Find("[data-testid=artwork-upload-input]").HasAttribute("disabled").ShouldBeTrue();
        pending.SetResult(ArtworkData.WithArtwork(ArtworkSourceDto.User));
        page.WaitForAssertion(() => page.FindAll("[data-testid=artwork-progress]").ShouldBeEmpty());
    }

    [Theory]
    [InlineData("image/gif", "own.gif", 10, ArtworkRules.UnsupportedTypeMessage)]
    [InlineData("", "own.bmp", 10, ArtworkRules.UnsupportedTypeMessage)]
    [InlineData("image/png", "big.png", (int)ContractLimits.MaxArtworkUploadBytes + 1, ArtworkRules.TooLargeMessage)]
    [InlineData("image/webp", "empty.webp", 0, ArtworkRules.EmptyMessage)]
    public void ClientChecks_RefuseBeforeTheUpload(string type, string name, int bytes, string message)
    {
        var page = RenderAdvanced();

        Choose(page, Png(bytes, type, name));

        page.Find("[data-testid=artwork-error]").TextContent.ShouldBe(message);
        Api.DidNotReceive().SetArtworkAsync(Arg.Any<Guid>(), Arg.Any<UploadFile>(), Arg.Any<CancellationToken>());
        page.Find("[data-testid=track-cover]").GetAttribute("data-artwork-source").ShouldBe("embedded");
    }

    [Fact]
    public void ExactlyTenMebibytes_IsAccepted() =>
        ArtworkRules.Validate("image/jpeg", ContractLimits.MaxArtworkUploadBytes).ShouldBeNull();

    [Fact]
    public void ServerRefusal_ShowsTheReadableReason_AndKeepsTheCurrentPicture()
    {
        Api.SetArtworkAsync(TestData.TrackId, Arg.Any<UploadFile>(), Arg.Any<CancellationToken>())
            .Returns<TrackDto>(_ => throw Problem(HttpStatusCode.BadRequest, "Not an image", "The file is not a readable picture."));
        var page = RenderAdvanced();

        Choose(page, Png());

        page.WaitForAssertion(() => page.Find("[data-testid=artwork-error]").TextContent.ShouldBe("The file is not a readable picture."));
        page.Find("[data-testid=track-cover]").GetAttribute("data-artwork-source").ShouldBe("embedded");
    }

    [Fact]
    public async Task Reset_GoesBackToTheAutomaticPicture()
    {
        Api.ResetArtworkAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(ArtworkData.WithArtwork(ArtworkSourceDto.Embedded));
        var page = RenderAdvanced(ArtworkData.WithArtwork(ArtworkSourceDto.User));

        await page.Find("[data-testid=artwork-reset]").ClickAsync(new());

        page.Find("[data-testid=track-cover]").GetAttribute("data-artwork-source").ShouldBe("embedded");
        page.FindAll("[data-testid=artwork-reset]").ShouldBeEmpty();
    }

    [Fact]
    public async Task ResetFailure_ShowsTheReason()
    {
        Api.ResetArtworkAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns<TrackDto>(_ => throw Problem(HttpStatusCode.NotFound, "Not found", "The track no longer exists."));
        var page = RenderAdvanced(ArtworkData.WithArtwork(ArtworkSourceDto.User));

        await page.Find("[data-testid=artwork-reset]").ClickAsync(new());

        page.Find("[data-testid=artwork-error]").TextContent.ShouldBe("The track no longer exists.");
    }

    [Fact]
    public void Actions_CarryTheArtworkHelpMarker() =>
        RenderAdvanced().Find("[data-help-id=artwork-actions]").QuerySelector("[data-testid=artwork-upload-input]").ShouldNotBeNull();
}
