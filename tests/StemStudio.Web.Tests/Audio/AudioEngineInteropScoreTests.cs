using Bunit;
using Microsoft.Extensions.Options;
using StemStudio.Web.Audio;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Audio;

/// <summary>Engine 2.4.0 calls of the wrapper (014): score follower methods and the <c>OnLoopPass</c> callback.</summary>
public sealed class AudioEngineInteropScoreTests : IAsyncDisposable
{
    private readonly BunitContext _context = new();
    private readonly BunitJSInterop _engine;
    private readonly AudioEngineInterop _interop;

    public AudioEngineInteropScoreTests()
    {
        _engine = _context.JSInterop.SetupModule("./js/audio-engine.js").SetupModule("createEngine", _ => true);
        _engine.Mode = JSRuntimeMode.Loose;
        _interop = new AudioEngineInterop(_context.JSInterop.JSRuntime, Options.Create(new StemStudioWebOptions()));
    }

    public async ValueTask DisposeAsync()
    {
        await _interop.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task ScoreFollower_AttachSetAndDetach_InvokeTheEngine()
    {
        await _interop.InitializeAsync();
        var map = ScoreViewMap.Of(PracticeFlowData.ScoreView());
        var options = new ScoreFollowOptions(true, ScrollPauseMs: 4000);

        await _interop.AttachScoreFollowerAsync("score-view", map, options);
        await _interop.SetScoreFollowOptionsAsync(options with { Follow = false });
        await _interop.DetachScoreFollowerAsync();

        _engine.VerifyInvoke("attachScoreFollower").Arguments.ShouldBe(["score-view", map, options]);
        _engine.VerifyInvoke("setScoreFollowOptions").Arguments.ShouldBe([options with { Follow = false }]);
        _engine.VerifyInvoke("detachScoreFollower");
        map.Bars.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Detach_WithoutAnEngine_DoesNothing()
    {
        await Should.NotThrowAsync(() => _interop.DetachScoreFollowerAsync());
        Should.Throw<ArgumentNullException>(() => ScoreViewMap.Of(null!));
    }

    [Fact]
    public void OnLoopPass_RaisesLoopPassed()
    {
        var passes = new List<LoopPassInfo>();
        _interop.LoopPassed += (_, pass) => passes.Add(pass);

        _interop.OnLoopPass(new LoopPassInfo(10, 30, 2, 85));

        passes.ShouldBe([new LoopPassInfo(10, 30, 2, 85)]);
        Should.NotThrow(() => new AudioEngineInterop(_context.JSInterop.JSRuntime, Options.Create(new StemStudioWebOptions())).OnLoopPass(new LoopPassInfo(0, 1, 1, 100)));
    }
}
