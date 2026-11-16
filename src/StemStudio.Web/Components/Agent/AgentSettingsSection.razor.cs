using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Agent;

/// <summary>Loads, tests and saves the agent settings; after a save the panel's status is reloaded.</summary>
public sealed partial class AgentSettingsSection
{
    [Inject]
    private IAgentApiClient Api { get; set; } = default!;

    [Inject]
    private AgentPanelState Panel { get; set; } = default!;

    private AgentSettingsForm? Form { get; set; }

    private AgentStatusDto? Status { get; set; }

    private BackendTestResultDto? TestResult { get; set; }

    private string? Error { get; set; }

    private bool Busy { get; set; }

    private bool Saved { get; set; }

    private bool KeyConfigured => Status?.Claude.KeyConfigured == true;

    private string OllamaText => Status?.Ollama is { Reachable: true } ollama
        ? $"Reachable, version {ollama.Version}. Models: {string.Join(", ", ollama.Models.Select(m => m.Tools ? m.Name : $"{m.Name} (no tools)"))}."
        : "Not reachable. Start Ollama on this computer.";

    private string ClaudeText => KeyConfigured
        ? "API key configured in the API's environment."
        : "Not configured: set ANTHROPIC_API_KEY in the environment of the StemStudio API (for example export it in the shell that starts StemStudio) and restart the API.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Error = await ApiCall.RunAsync(async () =>
        {
            Status = await Api.GetStatusAsync();
            Form = AgentSettingsForm.From(await Api.GetSettingsAsync());
        });
    }

    private static string Text(ChangeEventArgs e) => e.Value?.ToString() ?? string.Empty;

    private static string TestText(BackendTestResultDto result) =>
        $"{(result.Ok ? "Connected" : "Failed")} in {result.LatencyMs} ms: {result.Detail}";

    private void Select(AgentBackendDto backend)
    {
        Form!.Backend = backend;
        Saved = false;
    }

    private async Task TestAsync()
    {
        Busy = true;
        (TestResult, Error) = await ApiCall.TryAsync(() => Api.TestBackendAsync(Form!.Backend));
        Busy = false;
    }

    private async Task SaveAsync()
    {
        Error = Form!.Validate();
        if (Error is not null)
        {
            return;
        }

        Busy = true;
        Error = await ApiCall.RunAsync(async () =>
        {
            Form = AgentSettingsForm.From(await Api.UpdateSettingsAsync(Form.ToRequest()));
            await Panel.RefreshStatusAsync();
            Status = Panel.Status ?? Status;
        });
        Saved = Error is null;
        Busy = false;
    }
}
