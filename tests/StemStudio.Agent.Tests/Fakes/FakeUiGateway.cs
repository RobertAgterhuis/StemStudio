using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>A UI gateway that answers every request with a responder function and records the requests.</summary>
public sealed class FakeUiGateway : IUiToolGateway
{
    private readonly List<UiToolRequest> _requests = [];

    /// <summary>Answers a request; default: ok with an empty object.</summary>
    public Func<UiToolRequest, UiToolResult> Responder { get; set; } = _ => new UiToolResult { Ok = true, Result = "{}" };

    public IReadOnlyList<UiToolRequest> Requests => _requests;

    public Task<UiToolResult> RequestAsync(UiToolRequest request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Task.FromResult(Responder(request));
    }

    public bool TryComplete(Guid runId, Guid stepId, UiToolResult result) => false;
}
