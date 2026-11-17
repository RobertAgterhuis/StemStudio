using Microsoft.JSInterop;

namespace StemStudio.Web.Audio;

/// <summary>Score follower and loop passes of the engine wrapper (audio-engine 2.4.0, feature 014).</summary>
public sealed partial class AudioEngineInterop
{
    /// <summary><c>attachScoreFollower</c>.</summary>
    public const string AttachScoreFollower = "attachScoreFollower";

    /// <summary><c>setScoreFollowOptions</c>.</summary>
    public const string SetScoreFollowOptions = "setScoreFollowOptions";

    /// <summary><c>detachScoreFollower</c>.</summary>
    public const string DetachScoreFollower = "detachScoreFollower";

    /// <summary>The 2.4.0 score methods this wrapper invokes (checked against <c>types.ts</c>).</summary>
    public static readonly IReadOnlyList<string> ScoreMethods = [AttachScoreFollower, SetScoreFollowOptions, DetachScoreFollower];

    /// <inheritdoc />
    public event EventHandler<LoopPassInfo>? LoopPassed;

    /// <inheritdoc />
    public Task AttachScoreFollowerAsync(string elementId, ScoreViewMap view, ScoreFollowOptions options) =>
        Call(AttachScoreFollower, elementId, view, options);

    /// <inheritdoc />
    public Task SetScoreFollowOptionsAsync(ScoreFollowOptions options) => Call(SetScoreFollowOptions, options);

    /// <inheritdoc />
    public Task DetachScoreFollowerAsync() => _engine is null ? Task.CompletedTask : Call(DetachScoreFollower);

    /// <summary>Engine callback <c>OnLoopPass</c> (2.4.0): one completed pass of an enabled loop, when its wrap is heard.</summary>
    /// <param name="pass">The pass.</param>
    [JSInvokable]
    public void OnLoopPass(LoopPassInfo pass) => LoopPassed?.Invoke(this, pass);
}
