using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

public sealed class ImportPageTests : PageTestContext
{
    [Fact]
    public void Notice_And_LimitationHelp_AreVisible()
    {
        var page = Render<Import>();

        var notice = page.Find("[data-testid=legal-notice]").TextContent;
        notice.ShouldContain("for personal practice only");
        notice.ShouldContain("responsible for complying with copyright and the platforms' terms of service");
        page.Find("[data-testid=stem-limitation-help]").TextContent
            .ShouldContain("not reliably possible with current open-source models");
        page.FindAll("[data-testid=import-error]").ShouldBeEmpty();
        page.Find("[data-testid=import-file-input]").GetAttribute("accept").ShouldBe(".mp3,.wav,.flac");
    }

    [Fact]
    public async Task UrlSubmit_ImportsAndNavigatesToTrack()
    {
        Api.ImportUrlAsync("https://youtu.be/dQw4w9WgXcQ", Arg.Any<CancellationToken>()).Returns(TestData.Track());
        var page = Render<Import>();

        page.Find("[data-testid=import-url-input]").Change("  https://youtu.be/dQw4w9WgXcQ ");
        await page.Find("form").SubmitAsync();

        NavigatedTo.ShouldBe($"http://localhost/tracks/{TestData.TrackId}");
    }

    [Fact]
    public async Task EmptyUrl_ShowsMessage_WithoutCallingApi()
    {
        var page = Render<Import>();

        await page.Find("form").SubmitAsync();

        page.Find("[data-testid=import-error]").TextContent.ShouldBe("Paste a YouTube video link or a Spotify track link.");
        ImportCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidationProblem_ShowsFieldMessage()
    {
        Api.ImportUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(new ApiError(
            HttpStatusCode.BadRequest, "One or more validation errors occurred.", null,
            new Dictionary<string, string[]> { ["url"] = ["Playlists and albums are not supported."] })));
        var page = Render<Import>();

        page.Find("[data-testid=import-url-input]").Change("https://open.spotify.com/playlist/x");
        await page.Find("form").SubmitAsync();

        page.Find("[data-testid=import-error]").TextContent.ShouldBe("Playlists and albums are not supported.");
        NavigatedTo.ShouldBe("http://localhost/");
    }

    [Fact]
    public async Task WorkerUnavailable_ShowsServiceMessage()
    {
        Api.ImportUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(
            HttpStatusCode.ServiceUnavailable, "Processing service unavailable",
            "The processing service is unavailable. Existing tracks and mixes remain usable."));
        var page = Render<Import>();

        page.Find("[data-testid=import-url-input]").Change("https://youtu.be/dQw4w9WgXcQ");
        await page.Find("form").SubmitAsync();

        page.Find("[data-testid=import-error]").TextContent.ShouldContain("processing service is unavailable");
    }

    [Fact]
    public void Upload_LargeFile_IsStreamedAsStreamContent()
    {
        using var handler = new StubHttpMessageHandler()
            .Respond(HttpStatusCode.OK, """{ "reachable": false }""")
            .Respond(HttpStatusCode.Accepted, TestData.CompletedTrackJson);
        Services.AddSingleton<IStemStudioApiClient>(new StemStudioApiClient(StubHttpMessageHandler.CreateClient(handler)));
        var page = Render<Import>();

        page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[600 * 1024], "song.mp3", null, "audio/mpeg"));

        page.WaitForAssertion(() => NavigatedTo.ShouldBe($"http://localhost/tracks/{TestData.TrackId}"));
        handler.Requests[0].PathAndQuery.ShouldBe("/api/device");
        var part = handler.Requests.Skip(1).ShouldHaveSingleItem().Parts.ShouldHaveSingleItem();
        part.ContentType.ShouldBe(typeof(StreamContent));
        part.FileName.ShouldBe("song.mp3");
        part.Length.ShouldBe(600 * 1024);
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_IsRejectedClientSide()
    {
        var page = Render<Import>();

        page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("x", "notes.txt"));

        page.WaitForAssertion(() => page.Find("[data-testid=import-error]").TextContent.ShouldBe(UploadRules.UnsupportedTypeMessage));
        ImportCalls().ShouldBeEmpty();
    }

    [Fact]
    public void Upload_ProblemFromApi_IsShown()
    {
        Api.ImportUploadAsync(Arg.Any<UploadFile>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.UnsupportedMediaType, "Unsupported file type", "Only MP3, WAV and FLAC files are supported."));
        var page = Render<Import>();

        page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3], "song.wav"));

        page.WaitForAssertion(() => page.Find("[data-testid=import-error]").TextContent.ShouldBe("Only MP3, WAV and FLAC files are supported."));
    }

    [Fact]
    public void Upload_WithoutBrowserContentType_SendsOctetStream()
    {
        Api.ImportUploadAsync(Arg.Any<UploadFile>(), Arg.Any<CancellationToken>()).Returns(TestData.Track());
        var page = Render<Import>();

        page.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3], "song.FLAC", null, ""));

        page.WaitForAssertion(() => Api.Received().ImportUploadAsync(
            Arg.Is<UploadFile>(f => f.ContentType == "application/octet-stream" && f.FileName == "song.FLAC"), Arg.Any<CancellationToken>()));
    }

    [Theory]
    [InlineData("song.mp3", 1, null)]
    [InlineData("SONG.WAV", ContractLimits.MaxUploadBytes, null)]
    [InlineData("song.flac", ContractLimits.MaxUploadBytes + 1, UploadRules.TooLargeMessage)]
    [InlineData("song.flac", 0, UploadRules.EmptyMessage)]
    [InlineData("song.ogg", 10, UploadRules.UnsupportedTypeMessage)]
    [InlineData("song", 10, UploadRules.UnsupportedTypeMessage)]
    public void UploadRules_ValidateExtensionAndSize(string name, long size, string? expected)
    {
        UploadRules.Validate(name, size).ShouldBe(expected);
    }

    // Every call except the device query the page makes on load (feature 003).
    private IEnumerable<NSubstitute.Core.ICall> ImportCalls() =>
        Api.ReceivedCalls().Where(c => c.GetMethodInfo().Name != nameof(IStemStudioApiClient.GetDeviceAsync));
}
