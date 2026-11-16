using System.Text.Json;
using StemStudio.Contracts.Agent;

namespace StemStudio.Web.Agent;

/// <summary>
/// Layout-level UI tool host (DELTA.md section 4): runs a <c>ui.requested</c> tool on the open mixer (registered by
/// the mixer page for its track) or on the layout's navigation and help tools, and turns the outcome into the
/// <c>ui-results</c> body. A mixer tool without an open mixer, or for another track, fails readably.
/// </summary>
/// <param name="layout">The layout's tools.</param>
public sealed class AgentUiToolHost(NavigationUiTools layout)
{
    /// <summary>JSON options of tool results (camelCase, nulls kept: <c>previous: null</c> is information).</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string MixerNotOpen = "The mixer of the track is not open";
    private MixerUiTools? _mixer;

    /// <summary>True while a mixer page has registered its tools.</summary>
    public bool HasMixer => _mixer is not null;

    /// <summary>Registers the tools of an open mixer page; disposing the result removes them again.</summary>
    /// <param name="mixer">The page's mixer tools.</param>
    /// <returns>The registration.</returns>
    public IDisposable RegisterMixer(MixerUiTools mixer)
    {
        _mixer = mixer;
        return new Registration(() => _mixer = ReferenceEquals(_mixer, mixer) ? null : _mixer);
    }

    /// <summary>The mix loaded in the open mixer of <paramref name="trackId"/>, or null.</summary>
    /// <param name="trackId">The track.</param>
    /// <returns>The mix id.</returns>
    public Guid? OpenMixId(Guid trackId) => _mixer?.TrackId == trackId ? _mixer.MixId : null;

    /// <summary>Runs one requested UI tool.</summary>
    /// <param name="request">The <c>ui.requested</c> data.</param>
    /// <returns>The result body for <c>POST /api/agent/runs/{id}/ui-results/{stepId}</c>.</returns>
    public async Task<SubmitUiResultRequest> ExecuteAsync(UiRequestedEventDto request)
    {
        var arguments = new UiToolArguments(request.Arguments);
        var outcome = Route(request.Tool, arguments) is { } handler
            ? await handler.ExecuteAsync(request.Tool, arguments)
            : UiToolResult.Fail(Unhandled(request.Tool, arguments));
        return new SubmitUiResultRequest
        {
            Ok = outcome.Ok,
            Result = outcome.Result is null ? null : JsonSerializer.SerializeToElement(outcome.Result, Json),
            Error = outcome.Error is { Length: > AgentContractLimits.ErrorMaxLength } text ? text[..AgentContractLimits.ErrorMaxLength] : outcome.Error,
        };
    }

    private IAgentUiToolHandler? Route(string tool, UiToolArguments arguments)
    {
        if (layout.CanRun(tool))
        {
            return layout;
        }

        return _mixer is { } mixer && mixer.CanRun(tool) && IsFor(mixer, arguments) ? mixer : null;
    }

    private static bool IsFor(MixerUiTools mixer, UiToolArguments arguments) =>
        arguments.Id("trackId") is not { } trackId || trackId == mixer.TrackId;

    private string Unhandled(string tool, UiToolArguments arguments) => (MixerUiTools.Names.Contains(tool), _mixer) switch
    {
        (true, { } open) when arguments.Id("trackId") is not null => $"The mixer of that track is not open (the mixer of {open.Title} is)",
        (true, _) => MixerNotOpen,
        _ => $"No open page can run {tool}",
    };

    private sealed class Registration(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
}
