using System.Net;
using StemStudio.Api.Handouts;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.Tests.Handouts;

/// <summary>
/// The API mirrors the worker's four handout job limits (worker 1.11.0 <c>jobs.handout_limits</c>, 020 security fixes) and
/// refuses a document over them with a readable 422 <c>document</c> error before it submits anything.
/// </summary>
public sealed class HandoutJobLimitsTests : ApiTestBase
{
    private static readonly HandoutJobSize Small = new(1000, 100, 10, 0);

    [Fact]
    public void Limits_AreTheWorkersNumbers()
    {
        (ContractLimits.MaxHandoutRequestBytes, ContractLimits.MaxHandoutTextChars, ContractLimits.MaxHandoutLayoutCells, ContractLimits.MaxHandoutNotationBytes)
            .ShouldBe((8_388_608L, 500_000L, 30_000L, 134_217_728L));
    }

    [Fact]
    public void Measure_CountsLikeTheWorker()
    {
        var request = WorkerInsightsClientTests.HandoutRequest();
        var paths = request.Document.Songs[0].Notation.Select(item => item.PdfPath).ToList();

        var size = HandoutJobLimits.Measure(request, new Dictionary<string, long> { [paths[0]] = 700, [paths[1]] = 300 });

        // 2 sections + 4 bars + 4 chords in bars + 4 legend chords (worker layout_cells).
        size.Cells.ShouldBe(14);
        size.NotationBytes.ShouldBe(1000);
        size.BodyBytes.ShouldBe(WorkerClient.HandoutBody(request).Length);
        size.TextChars.ShouldBeGreaterThan(request.Document.Notice.Full.Length + request.Document.Songs[0].PracticeNotes!.Length);
        size.TextChars.ShouldBeLessThan(size.BodyBytes);
    }

    [Fact]
    public void Measure_CountsLyricLinesTheirChordsAndSetlistEntries_AndEachNotationFileOnce()
    {
        var request = WorkerInsightsClientTests.HandoutRequest();
        var song = request.Document.Songs[0];
        var lines = new List<WorkerLyricLine>
        {
            new("Hello there", 0, [new WorkerLyricChord("Am", 0), new WorkerLyricChord("F", 6)]),
            new("General", null, []),
        };
        var entries = new List<WorkerSetlistEntryBlock> { new(1, "Example Song", null, null, null, null, null), new(2, "Other", null, null, null, null, null) };
        var twice = song with { Lyrics = new WorkerHandoutLyrics(lines, "word", "LRCLIB"), Notation = [song.Notation[0], song.Notation[0]] };
        var setlist = request with
        {
            Document = request.Document with { Kind = Domain.Handouts.HandoutKind.Setlist, Setlist = new WorkerSetlistBlock("Night", null, entries), Songs = [twice] },
        };

        var size = HandoutJobLimits.Measure(setlist, new Dictionary<string, long> { [song.Notation[0].PdfPath] = 700 });

        size.Cells.ShouldBe(14 + 2 + 2 + 2);
        size.NotationBytes.ShouldBe(700);
    }

    [Fact]
    public void Measure_CountsCharactersNotUtf16Units()
    {
        var request = WorkerInsightsClientTests.HandoutRequest();
        var plain = HandoutJobLimits.Measure(request, new Dictionary<string, long>());
        var song = request.Document.Songs[0];
        var notes = request with { Document = request.Document with { Songs = [song with { PracticeNotes = song.PracticeNotes + "\U0001F3B8" }] } };

        HandoutJobLimits.Measure(notes, new Dictionary<string, long>()).TextChars.ShouldBe(plain.TextChars + 1);
    }

    [Fact]
    public void WithinTheLimits_NothingIsRefused()
    {
        HandoutJobLimits.Refusal(Small).ShouldBeNull();
        HandoutJobLimits.Refusal(new(ContractLimits.MaxHandoutRequestBytes, ContractLimits.MaxHandoutTextChars, ContractLimits.MaxHandoutLayoutCells, ContractLimits.MaxHandoutNotationBytes))
            .ShouldBeNull();
    }

    [Theory]
    [InlineData(0, "The handout is too large to send to the processing service (over 8 MiB). Leave out lyrics or songs.")]
    [InlineData(1, "The handout has more than 500,000 characters of text. Leave out lyrics or songs.")]
    [InlineData(2, "The handout has more than 30,000 sections, bars, chords and lyric lines. Leave out ingredients or songs.")]
    [InlineData(3, "The sheet music of the handout is larger than 128 MiB together. Choose fewer sheet music items.")]
    public void OverALimit_IsRefusedWithAReadableText(int limit, string text)
    {
        var size = limit switch
        {
            0 => Small with { BodyBytes = ContractLimits.MaxHandoutRequestBytes + 1 },
            1 => Small with { TextChars = ContractLimits.MaxHandoutTextChars + 1 },
            2 => Small with { Cells = ContractLimits.MaxHandoutLayoutCells + 1 },
            _ => Small with { NotationBytes = ContractLimits.MaxHandoutNotationBytes + 1 },
        };

        HandoutJobLimits.Refusal(size).ShouldBe(text);
    }

    [Fact]
    public async Task CreateHandout_OverTheNotationLimit_Answers422_AndSubmitsNothing()
    {
        var song = await Factory.SeedSongAsync();
        await Factory.SeedAssetsAsync(HandoutTestData.Score(song.Id, ("bass", ScoreProfile.Bass), 1, sizeBytes: ContractLimits.MaxHandoutNotationBytes + 1));

        using var response = await Client.PostHandoutAsync(new CreateHandoutRequest { Source = HandoutSourceDto.Song(song.Id), Family = HandoutFamilyDto.Bass });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "createHandout");
        (await response.FieldErrorsAsync("document")).Single().ShouldBe("The sheet music of the handout is larger than 128 MiB together. Choose fewer sheet music items.");
        Factory.InsightsWorker.HandoutJobs.ShouldBeEmpty();
    }
}
