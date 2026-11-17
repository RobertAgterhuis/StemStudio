using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.LyricAssist.Fakes;

/// <summary>
/// Deterministic <see cref="ILyricModel"/> for tests and the E2E fake stack: answers block <c>n</c> with the script's block
/// <c>n</c> (the same text on a retry), and the empty output beyond the script. Records every request.
/// </summary>
/// <param name="script">The script.</param>
public sealed class ScriptedLyricModel(LyricScript script) : ILyricModel, ILyricModelSelection
{
    /// <summary>The model reported for every reply.</summary>
    public static LyricModelInfo Info { get; } = new() { Backend = AgentBackendDto.Fake, Model = "scripted", UsingFallback = false };

    private readonly List<LyricModelRequest> _requests = [];

    /// <summary>The requests received, in order.</summary>
    public IReadOnlyList<LyricModelRequest> Requests => _requests;

    /// <inheritdoc/>
    public Task<LyricModelInfo> SelectAsync(CancellationToken cancellationToken) => Task.FromResult(Info);

    /// <inheritdoc/>
    public Task<LyricModelReply> CompleteAsync(LyricModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Add(request);
        return Task.FromResult(new LyricModelReply { Json = script.OutputFor(request.BlockIndex), Model = Info });
    }
}
