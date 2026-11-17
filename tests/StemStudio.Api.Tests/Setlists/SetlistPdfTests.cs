using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Setlists;
using StemStudio.Api.Tests.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Setlists;
using static StemStudio.Api.Tests.Setlists.SetlistTestData;

namespace StemStudio.Api.Tests.Setlists;

/// <summary>Feature 014 T090: the setlist PDF - content, options, waiting for the worker, timeout and file name (FR-018).</summary>
public sealed class SetlistPdfTests : ApiTestBase
{
    [Fact]
    public async Task Pdf_SendsTheContent_ReturnsTheFile_AndDeletesTheWorkerJob()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        await Factory.SeedDetectedAsync(track.Id);
        var mix = await Factory.SeedMixAsync(track);
        (await Client.PutAsJsonAsync(ApiRoutes.Lyrics(track.Id), new SetLyricsRequest("First line\nSecond line\n\nChorus line"), Ct)).Dispose();
        var setlist = await Client.CreateSetlistAsync("Band night: Friday/Saturday");
        (await Client.PutEntriesAsync(setlist.Id, Entry(track.Id, mix.Id, "Start soft."))).Dispose();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.SetlistPdf(setlist.Id, SetlistPdfOptions.Default), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe("Band night_ Friday_Saturday.pdf");
        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Factory.PracticeWorker.Pdf);
        var sent = Factory.PracticeWorker.DocumentRequests.Single();
        (sent.Title, sent.Note, sent.Options).ShouldBe(("Band night: Friday/Saturday", "Bring the capo.", new WorkerSetlistOptions(true, true, true, true)));
        var song = sent.Songs.Single();
        (song.Position, song.Bpm, song.MixName, song.Note, song.DurationSeconds).ShouldBe((1, (double?)113.5, "Band night", "Start soft.", 213.04));
        song.Sections.Select(section => section.Kind).ShouldBe(["intro", "verse", "chorus", "solo", "outro"]);
        song.Lyrics.ShouldBe(new WorkerSetlistLyrics(["First line", "Second line", "", "Chorus line"], false), new LyricsComparer());
        Factory.PracticeWorker.Deleted.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Pdf_OptionsComeFromTheQuery()
    {
        var setlist = await SetlistWithOneSongAsync();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.SetlistPdf(setlist.Id, new SetlistPdfOptions(false, true, false, false)), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.PracticeWorker.DocumentRequests.Single().Options.ShouldBe(new WorkerSetlistOptions(false, true, false, false));
    }

    [Fact]
    public async Task Pdf_WaitsForTheJobWithPolling()
    {
        var setlist = await SetlistWithOneSongAsync();
        Factory.PracticeWorker.DocumentReadsUntilDone = 3;

        var request = Client.GetAsync(new Uri(ApiRoutes.SetlistPdf(setlist.Id, SetlistPdfOptions.Default), UriKind.Relative), Ct);
        using var response = await AdvanceUntilDoneAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.PracticeWorker.Calls.Count(call => call.StartsWith("get-document:", StringComparison.Ordinal)).ShouldBe(4);
    }

    [Fact]
    public async Task Pdf_JobThatNeverFinishes_Is504_AndTheJobIsDeleted()
    {
        var setlist = await SetlistWithOneSongAsync();
        Factory.PracticeWorker.DocumentReadsUntilDone = -1;

        using var response = await AdvanceUntilDoneAsync(Client.GetAsync(new Uri(ApiRoutes.SetlistPdf(setlist.Id, SetlistPdfOptions.Default), UriKind.Relative), Ct));

        await SectionEndpointsTests.AssertProblemAsync(response, HttpStatusCode.GatewayTimeout, "document-timeout");
        Factory.PracticeWorker.Deleted.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Pdf_FailedJob_Is503WithTheWorkerText()
    {
        var setlist = await SetlistWithOneSongAsync();
        Factory.PracticeWorker.DocumentReadsUntilDone = -1;
        var request = Client.GetAsync(new Uri(ApiRoutes.SetlistPdf(setlist.Id, SetlistPdfOptions.Default), UriKind.Relative), Ct);
        await Eventually.WaitUntilAsync(() => Task.FromResult(!Factory.PracticeWorker.DocumentJobs.IsEmpty));
        Factory.PracticeWorker.SetDocument(Factory.PracticeWorker.DocumentJobs.Keys.Single(), WorkerPracticeJobState.Failed, "Fonts missing.");

        using var response = await AdvanceUntilDoneAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe("Fonts missing.");
    }

    [Fact]
    public async Task Pdf_EmptySetlist_Is409_UnknownIs404_WorkerDownIs503()
    {
        var empty = await Client.CreateSetlistAsync();
        var filled = await SetlistWithOneSongAsync();

        using var emptyResponse = await Client.GetAsync(new Uri(ApiRoutes.SetlistPdf(empty.Id, SetlistPdfOptions.Default), UriKind.Relative), Ct);
        using var unknown = await Client.GetAsync(new Uri(ApiRoutes.SetlistPdf(Guid.NewGuid(), SetlistPdfOptions.Default), UriKind.Relative), Ct);
        Factory.PracticeWorker.Unavailable = true;
        using var down = await Client.GetAsync(new Uri(ApiRoutes.SetlistPdf(filled.Id, SetlistPdfOptions.Default), UriKind.Relative), Ct);

        await SectionEndpointsTests.AssertProblemAsync(emptyResponse, HttpStatusCode.Conflict, "setlist-empty");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await SectionEndpointsTests.AssertProblemAsync(down, HttpStatusCode.ServiceUnavailable, "worker-unavailable");
    }

    [Theory]
    [InlineData("Friday rehearsal", "Friday rehearsal.pdf")]
    [InlineData("../../etc/passwd", "etc_passwd.pdf")]
    [InlineData("\"quoted\"\r\nX-Header: 1", "quoted___X-Header_ 1.pdf")]
    [InlineData("...", "setlist.pdf")]
    [InlineData("Ünïcødé ♪", "Ünïcødé.pdf")]
    public void FileName_IsSanitised(string name, string expected) => SetlistPdfService.FileName(name).ShouldBe(expected);

    [Fact]
    public void FileName_IsAtMost80CharactersBeforeTheExtension() => SetlistPdfService.FileName(new string('a', 200)).ShouldBe(new string('a', 80) + ".pdf");

    [Fact]
    public void Lyrics_AreCutAt10000Characters_AndMarkedShortened()
    {
        var text = string.Join('\n', Enumerable.Repeat(new string('x', 99), 120));

        var lyrics = SetlistContentBuilder.Lyrics(text)!;

        lyrics.Shortened.ShouldBeTrue();
        lyrics.Lines.Sum(line => line.Length).ShouldBe(SetlistContentBuilder.MaxLyricsCharacters);
        SetlistContentBuilder.Lyrics("  ").ShouldBeNull();
        SetlistContentBuilder.Lyrics("a\r\nb")!.ShouldSatisfyAllConditions(short_ => short_.Lines.ShouldBe(["a", "b"]), short_ => short_.Shortened.ShouldBeFalse());
    }

    private async Task<SetlistDto> SetlistWithOneSongAsync()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        var setlist = await Client.CreateSetlistAsync();
        (await Client.PutEntriesAsync(setlist.Id, Entry(track.Id))).Dispose();
        return setlist;
    }

    /// <summary>Advances the fake clock by the poll interval until the request has finished (the service waits on that clock).</summary>
    private async Task<HttpResponseMessage> AdvanceUntilDoneAsync(Task<HttpResponseMessage> request)
    {
        for (var step = 0; step < 1000 && !request.IsCompleted; step++)
        {
            await Task.Delay(5, Ct);
            Factory.Clock.Advance(SetlistPdfService.PollInterval);
        }

        return await request;
    }

    private sealed class LyricsComparer : IEqualityComparer<WorkerSetlistLyrics?>
    {
        public bool Equals(WorkerSetlistLyrics? x, WorkerSetlistLyrics? y) =>
            x is not null && y is not null && x.Shortened == y.Shortened && x.Lines.SequenceEqual(y.Lines);

        public int GetHashCode(WorkerSetlistLyrics? obj) => obj?.Lines.Count ?? 0;
    }
}
