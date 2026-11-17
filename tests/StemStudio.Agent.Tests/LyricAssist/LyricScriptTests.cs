using StemStudio.Agent.LyricAssist;
using StemStudio.Agent.LyricAssist.Fakes;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>023 Phase 0 (T007): the scripted assistant output (contracts/DELTA.md section 4) and the scripted model.</summary>
public sealed class LyricScriptTests : IDisposable
{
    private const string TwoBlocks = """{"version": 1, "blocks": [{"items": []}, {"items": [{"type": "spelling.typo", "severity": "error", "line": 3, "original": "wiet", "proposal": "weet", "explanation": "Spelfout."}]}]}""";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "stemstudio-lyric-scripts-" + Guid.NewGuid().ToString("N"));

    public LyricScriptTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void Parse_KeepsEachBlockAsJsonText()
    {
        var script = LyricScript.Parse(TwoBlocks);

        script.Blocks.Count.ShouldBe(2);
        (script.OutputFor(0), script.OutputFor(1)).ShouldBe(("""{"items":[]}""", """{"items":[{"type":"spelling.typo","severity":"error","line":3,"original":"wiet","proposal":"weet","explanation":"Spelfout."}]}"""));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(40)]
    public void OutputFor_BeyondTheScript_IsEmpty(int blockIndex)
    {
        LyricScript.Parse(TwoBlocks).OutputFor(blockIndex).ShouldBe(LyricScript.EmptyOutput);
    }

    [Theory]
    [InlineData("""{"blocks": []}""")]
    [InlineData("""{"version": 2, "blocks": []}""")]
    [InlineData("""{"version": "1", "blocks": []}""")]
    [InlineData("""{"version": 1}""")]
    [InlineData("""{"version": 1, "blocks": {}}""")]
    [InlineData("""{"version": 1, "blocks": [1]}""")]
    [InlineData("""[]""")]
    [InlineData("""not json""")]
    public void Parse_RejectsAnythingButAVersion1Script(string json)
    {
        Should.Throw<InvalidDataException>(() => LyricScript.Parse(json));
    }

    [Fact]
    public void Load_ReadsAFile()
    {
        var path = Write("one.json", TwoBlocks);

        LyricScript.Load(path).Blocks.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("Zomeravond", "zomeravond")]
    [InlineData("  Radio (live) - take 2!", "radio-live-take-2")]
    [InlineData("Één nacht", "n-nacht")]
    [InlineData("---", "")]
    public void Slug_KeepsAsciiLettersAndDigits(string title, string slug)
    {
        LyricScript.Slug(title).ShouldBe(slug);
    }

    [Fact]
    public void Resolve_AFileIsUsedAsIs()
    {
        var path = Write("any.json", TwoBlocks);

        LyricScript.Resolve(path, "Whatever", AssistKindDto.Titles).ShouldBe(path);
    }

    [Fact]
    public void Resolve_InAFolder_PrefersTitleAndKindThenKind()
    {
        var specific = Write("zomeravond-check.json", TwoBlocks);
        var general = Write("check.json", TwoBlocks);

        LyricScript.Resolve(_folder, "Zomeravond", AssistKindDto.Check).ShouldBe(specific);
        LyricScript.Resolve(_folder, "Radio", AssistKindDto.Check).ShouldBe(general);
        LyricScript.Resolve(_folder, "Radio", AssistKindDto.Continue).ShouldBeNull();
    }

    [Fact]
    public async Task ScriptedModel_AnswersByBlockIndexAndRecordsRequests()
    {
        var model = new ScriptedLyricModel(LyricScript.Parse(TwoBlocks));

        var first = await model.CompleteAsync(Request(1), TestContext.Current.CancellationToken);
        var retry = await model.CompleteAsync(Request(1), TestContext.Current.CancellationToken);
        var beyond = await model.CompleteAsync(Request(5), TestContext.Current.CancellationToken);

        (first.Json, retry.Json, beyond.Json).ShouldBe((LyricScript.Parse(TwoBlocks).OutputFor(1), first.Json, LyricScript.EmptyOutput));
        (first.Model, first.Usage).ShouldBe((ScriptedLyricModel.Info, null));
        model.Requests.Select(r => r.BlockIndex).ShouldBe([1, 1, 5]);
    }

    [Fact]
    public void ScriptedModel_IsTheFakeBackend()
    {
        (ScriptedLyricModel.Info.Backend, ScriptedLyricModel.Info.UsingFallback).ShouldBe((AgentBackendDto.Fake, false));
    }

    [Fact]
    public async Task ScriptedModel_HonoursCancellation()
    {
        var model = new ScriptedLyricModel(LyricScript.Parse(TwoBlocks));

        await Should.ThrowAsync<OperationCanceledException>(() => model.CompleteAsync(Request(0), new CancellationToken(canceled: true)));
        model.Requests.ShouldBeEmpty();
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static LyricModelRequest Request(int block) =>
        new() { Kind = AssistKindDto.Check, BlockIndex = block, SystemPrompt = "s", UserPrompt = "u", Temperature = 0.2 };

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, text);
        return path;
    }
}
