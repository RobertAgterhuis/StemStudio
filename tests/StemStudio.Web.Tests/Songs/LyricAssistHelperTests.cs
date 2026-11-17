using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Lyrics.Assist;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>Pure helpers of the lyric-assistant UI: positions, highlights, texts, the board, the rhythm form and the counter.</summary>
public sealed class LyricAssistHelperTests
{
    [Theory]
    [InlineData("a\nb\r\nc", 0, 1)]
    [InlineData("a\nb\r\nc", 2, 2)]
    [InlineData("a\nb\r\nc", 99, 3)]
    [InlineData("", -5, 1)]
    public void LineAt_CountsLines(string text, int offset, int line) => SheetText.LineAt(text, offset).ShouldBe(line);

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 8)]
    [InlineData(9, 39)]
    public void OffsetOf_FindsTheLineStart(int line, int offset) => SheetText.OffsetOf(Text, line).ShouldBe(offset);

    [Theory]
    [InlineData("[verse]\na\nb\n\nc", 2, 3)]
    [InlineData("[verse]\na\nb\n\nc", 5, 5)]
    [InlineData("a", 1, 1)]
    public void BlockEnd_StopsBeforeABlankLine(string text, int line, int end) => SheetText.BlockEnd(text, line).ShouldBe(end);

    [Theory]
    [InlineData("de nacht is", 5, "nacht")]
    [InlineData("'k heb", 1, "k")]
    [InlineData("a  b", 2, null)]
    [InlineData("", 0, null)]
    public void WordAt_FindsTheWord(string text, int offset, string? word) => SheetText.WordAt(text, offset).ShouldBe(word);

    [Theory]
    [InlineData("[chorus]", true)]
    [InlineData(" [verse 2] ", true)]
    [InlineData("[", false)]
    [InlineData("la", false)]
    public void IsMarker(string line, bool marker) => SheetText.IsMarker(line).ShouldBe(marker);

    [Fact]
    public void Segments_SpellTheLine_AndSkipOverlapsAndMismatches()
    {
        const string line = "De nacht zijn warm en stil";
        var overlap = GrammarFix() with { SuggestionId = Guid.NewGuid(), Anchor = new AnchorDto { Line = 3, Start = 10, Length = 6, Original = "ijn wa" } };
        var wrong = DeviceIdea() with { Anchor = new AnchorDto { Line = 3, Start = 14, Length = 4, Original = "koud" } };
        var outside = DeviceIdea() with { SuggestionId = Guid.NewGuid(), Anchor = new AnchorDto { Line = 3, Start = 40, Length = 4, Original = "warm" } };
        var insert = GrammarFix() with { SuggestionId = Guid.NewGuid(), Operation = SuggestionOperationDto.InsertAfter };

        var pieces = HighlightLayout.Segments(line, [GrammarFix(), overlap, wrong, outside, insert]);

        string.Concat(pieces.Select(p => p.Text)).ShouldBe(line);
        pieces.Where(p => p.Suggestion is not null).Select(p => p.Text).ShouldBe(["zijn"]);
        HighlightLayout.StyleClass(SuggestionCategoryDto.Licence).ShouldBe("ss-hl-licence");
    }

    [Theory]
    [InlineData(SuggestionTypeDto.SpellingUnknownWord, "Spelling: unknown word")]
    [InlineData(SuggestionTypeDto.DeviceInternalRhyme, "Device: internal rhyme")]
    [InlineData(SuggestionTypeDto.WriteAlternative, "Writing help: alternative")]
    [InlineData(SuggestionTypeDto.LicenceCodeSwitch, "Poetic licence: code switch")]
    public void Type_IsReadable(SuggestionTypeDto type, string label) => AssistText.Type(type).ShouldBe(label);

    [Theory]
    [InlineData(AssistKindDto.Check, 1, "Checking 1 line")]
    [InlineData(AssistKindDto.Devices, 40, "Looking for devices in 40 lines")]
    [InlineData(AssistKindDto.Alternatives, 4, "Writing alternatives")]
    [InlineData(AssistKindDto.Continue, 4, "Writing a continuation")]
    [InlineData(AssistKindDto.Titles, 4, "Writing title ideas")]
    public void Started_Announcements(AssistKindDto kind, int lines, string text) => AssistText.Started(kind, lines).ShouldBe(text);

    [Fact]
    public void Finished_ListsTheCounts()
    {
        var counts = CategoryCountsDto.None with { Spelling = 2, Grammar = 1, Flow = 3, Device = 1 };

        AssistText.Finished(AssistKindDto.Check, counts).ShouldBe("Check finished: 7 suggestions - 2 spelling, 1 grammar, 3 flow, 1 device");
        AssistText.Finished(AssistKindDto.Titles, CategoryCountsDto.None with { Title = 1 }).ShouldBe("Title ideas finished: 1 suggestion - 1 title");
        AssistText.Finished(AssistKindDto.Devices, CategoryCountsDto.None).ShouldBe("Device suggestions finished: 0 suggestions");
        AssistText.Finished(AssistKindDto.Continue, CategoryCountsDto.None with { Licence = 1, Alternative = 1, Continuation = 1 })
            .ShouldBe("Continuation finished: 3 suggestions - 1 poetic licence, 1 alternative, 1 continuation");
        AssistText.KindName(AssistKindDto.Alternatives).ShouldBe("Alternatives");
    }

    [Fact]
    public void Focused_And_Withheld_And_Syllables()
    {
        AssistText.Focused(DeviceIdea() with { Anchor = DeviceIdea().Anchor with { Line = 0 }, Proposal = null }).ShouldBe("Title, device idea: warm. Uitleg bij de suggestie.");
        AssistText.Withheld(2).ShouldBe("2 suggestions withheld (too close to existing lyrics)");
        AssistText.Syllables(SpellingFix()).ShouldBeNull();
    }

    [Fact]
    public void Board_FiltersOrdersAndMoves()
    {
        var board = new SuggestionBoard();
        var alt1 = Suggestion(Guid.Parse("a0000000-0000-4000-8000-000000000001"), SuggestionTypeDto.WriteAlternative, SpellingFix().Anchor, "x") with { GroupId = Guid.Empty };
        var alt2 = alt1 with { SuggestionId = Guid.Parse("a0000000-0000-4000-8000-000000000002") };
        board.Merge([DeviceIdea(), SpellingFix(), alt1, alt2, GrammarFix() with { Status = SuggestionStatusDto.Rejected }]);

        board.Open.Select(s => s.Anchor.Line).ShouldBe([2, 2, 2, 3]);
        board.AcceptAllIds.Count.ShouldBe(3);
        board.Move(-1)!.SuggestionId.ShouldBe(Device);
        board.Move(1)!.SuggestionId.ShouldBe(board.Open[0].SuggestionId);
        board.Toggle(SuggestionCategoryDto.Device);
        board.Active.ShouldBeNull();
        board.Move(1)!.SuggestionId.ShouldBe(Device);
        board.Toggle(SuggestionCategoryDto.Device);
        board.Filter.ShouldBeEmpty();
        board.Toggle(SuggestionCategoryDto.Title);
        board.Move(1).ShouldBeNull();
        board.Toggle(null);
        board.OnLine(3).ShouldHaveSingleItem();
        board.Clear();
        (board.Open.Count, board.Active).ShouldBe((0, null));
    }

    [Theory]
    [InlineData("92 4 2 1.5", true)]
    [InlineData("39 4 2 1.5", false)]
    [InlineData("92 8 2 1.5", false)]
    [InlineData("92 4 9 1.5", false)]
    [InlineData("92 4 2 1.3", false)]
    [InlineData("92 4 2 4.25", false)]
    public void RhythmForm_ValidatesTheRanges(string values, bool valid)
    {
        var v = values.Split(' ').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        var form = new RhythmForm(null) { Bpm = (int)v[0], BeatsPerBar = (int)v[1], BarsPerLine = (int)v[2], SyllablesPerBeat = v[3] };

        (form.ToTarget() is not null, form.Problem is null).ShouldBe((valid, valid));
    }

    [Fact]
    public void RhythmForm_StartsFromTheTarget_AndDensityText()
    {
        new RhythmForm(new RhythmTargetDto { Bpm = 120, BeatsPerBar = 3, BarsPerLine = 1, SyllablesPerBeat = 2 }).ToTarget()!.Bpm.ShouldBe(120);
        RhythmForm.DensityText(2.04, null).ShouldBe("2.0 words per second");
        RhythmForm.DensityText(2.5, 65).ShouldBe("2.5 words per second at 1:05");
    }

    [Theory]
    [InlineData(AssistKindDto.Check, "0-0", "")]
    [InlineData(AssistKindDto.Check, "10-45", "2-3")]
    [InlineData(AssistKindDto.Devices, "45-10", "2-3")]
    [InlineData(AssistKindDto.Alternatives, "45-45", "3-3")]
    [InlineData(AssistKindDto.Continue, "10-10", "2-3")]
    [InlineData(AssistKindDto.Titles, "10-45", "")]
    public void Range_FollowsTheKind(AssistKindDto kind, string selection, string lines)
    {
        var (start, end) = Pair(selection);
        var range = EditorHost.Range(kind, Text, (start, end));

        (range.From is null ? string.Empty : $"{range.From}-{range.To}").ShouldBe(lines);
    }

    private static (int, int) Pair(string text) => (int.Parse(text.Split('-')[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(text.Split('-')[1], System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public async Task Host_SelectedText_TellsAWholeLine()
    {
        var host = new EditorHost(new LyricAssistSession(Substitute.For<IStemStudioApiClient>(), new EditorDocument(Sheet())), () => Task.FromResult((8, 38)));
        var word = new EditorHost(host.Session, () => Task.FromResult((50, 50)));

        (await host.SelectedTextAsync()).ShouldBe(("Ik wiet niet waar de wind gaat", true));
        (await word.SelectedTextAsync()).ShouldBe((string.Empty, false));
        (await word.WordAtCursorAsync()).ShouldBe("zijn");
        (await host.CountSyllablesAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Counter_WaitsForThePause_AndTheNewestWins()
    {
        var api = Substitute.For<IStemStudioApiClient>();
        api.CountSyllablesAsync(Arg.Any<SyllableRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SyllableReportDto { Language = AssistLanguageDto.Nl, Lines = [new SyllableLineDto { Line = 2, Syllables = 8, IsMarker = false, Deviates = false }] });
        var time = new FakeTimeProvider();
        using var counter = new SyllableCounter(api, time);

        var first = counter.CountAsync(AssistLanguageDto.Nl, "a", null);
        var second = counter.CountAsync(AssistLanguageDto.Nl, "b", null);
        time.Advance(SyllableCounter.Pause);

        (await first, await second).ShouldBe((false, true));
        await api.Received(1).CountSyllablesAsync(Arg.Is<SyllableRequest>(r => r.Text == "b"), Arg.Any<CancellationToken>());
        counter.Line(2)!.Syllables.ShouldBe(8);
        counter.Line(3).ShouldBeNull();
    }

    [Fact]
    public async Task Counter_Failure_KeepsTheOldReport()
    {
        var api = Substitute.For<IStemStudioApiClient>();
        api.CountSyllablesAsync(Arg.Any<SyllableRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(ApiError.Unreachable));
        using var counter = new SyllableCounter(api, TimeProvider.System);

        (await counter.CountAsync(AssistLanguageDto.En, "a", null, wait: false)).ShouldBeFalse();
        counter.Report.ShouldBeNull();
    }

    [Fact]
    public void Run_Percent_AndSnapshotDuration()
    {
        var running = AssistRun.From(Assist(AssistStatusDto.Running) with { BlocksTotal = 0 });
        running.Percent.ShouldBe(0);
        var done = AssistRun.From(Assist(AssistStatusDto.Completed) with
        {
            BlocksTotal = 0,
            Backend = null,
            StartedAt = DateTimeOffset.UnixEpoch,
            EndedAt = DateTimeOffset.UnixEpoch.AddSeconds(2),
        });
        (done.Percent, done.Backend, done.DurationMs).ShouldBe((100, null, 2000L));
    }

    [Fact]
    public void OwnWorkBadge_And_LogTexts()
    {
        LogText.Tokens(null).ShouldBe("-");
        LogText.Cause(RevisionCauseDto.Undo).ShouldBe("undo");
        LogText.Cause(RevisionCauseDto.Accept).ShouldBe("accepted suggestions");
    }
}
