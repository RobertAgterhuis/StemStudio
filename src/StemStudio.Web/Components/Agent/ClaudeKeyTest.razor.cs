using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Agent;

/// <summary>Runs the Claude key test on an explicit click with the chosen key source (backend API 1.21.0).</summary>
public sealed partial class ClaudeKeyTest
{
    private ClaudeKeyTestSourceDto _source = ClaudeKeyTestSourceDto.Active;
    private BackendTestResultDto? _result;
    private bool _busy;

    /// <summary>The key status; decides whether the test and the source choice are offered.</summary>
    [Parameter, EditorRequired]
    public ClaudeKeyStatusDto Status { get; set; } = ClaudeKeyStatusDto.NotConfigured;

    /// <summary>Disables the test (while the section is busy or when local-only applies).</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Raised when the test call itself failed (not when Anthropic refused the key: that is a result).</summary>
    [Parameter]
    public EventCallback<ApiError> Failed { get; set; }

    [Inject]
    private IAgentApiClient Api { get; set; } = default!;

    private async Task TestAsync()
    {
        _busy = true;
        try
        {
            _result = await Api.TestClaudeKeyAsync(_source);
        }
        catch (ApiException ex)
        {
            await Failed.InvokeAsync(ex.Error);
        }

        _busy = false;
    }
}
