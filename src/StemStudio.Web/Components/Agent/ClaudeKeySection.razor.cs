using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Agent;

/// <summary>
/// Stores the Claude key (backend API 1.21.0). The typed value lives only in <see cref="_value"/> until the save call and is
/// dropped before the call returns; the status shown afterwards comes from the API's answer.
/// </summary>
public sealed partial class ClaudeKeySection
{
    private string _value = string.Empty;
    private int _generation;

    /// <summary>The key status the agent status carried when the page loaded.</summary>
    [Parameter, EditorRequired]
    public ClaudeKeyStatusDto Initial { get; set; } = ClaudeKeyStatusDto.NotConfigured;

    /// <summary>Raised with the new status after a key was stored, replaced or deleted.</summary>
    [Parameter]
    public EventCallback<ClaudeKeyStatusDto> Changed { get; set; }

    [Inject]
    private IAgentApiClient Api { get; set; } = default!;

    private ClaudeKeyStatusDto Key { get; set; } = ClaudeKeyStatusDto.NotConfigured;

    private string? Error { get; set; }

    private bool Busy { get; set; }

    private bool ShowsInput => Key.Stored == StoredClaudeKeyStateDto.Absent;

    private string SaveText => Key.Stored == StoredClaudeKeyStateDto.Absent ? "Save key" : "Save new key";

    private bool CanSave => !Busy && Key.StoreAvailable && !string.IsNullOrWhiteSpace(_value);

    /// <inheritdoc />
    protected override void OnInitialized() => Key = Initial;

    private void OnInput(ChangeEventArgs e) => _value = e.Value?.ToString() ?? string.Empty;

    private void ClearInput()
    {
        _value = string.Empty;
        _generation++;
    }

    private async Task SaveAsync()
    {
        var value = _value;
        ClearInput();
        Busy = true;
        await ApplyAsync(() => Api.StoreClaudeKeyAsync(value));
        Busy = false;
    }

    private async Task ApplyAsync(Func<Task<ClaudeKeyStatusDto>> call)
    {
        try
        {
            Key = await call();
            Error = null;
            await Changed.InvokeAsync(Key);
        }
        catch (ApiException ex)
        {
            Error = ClaudeKeyErrors.Message(ex.Error);
        }
    }
}
