using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Agent;

/// <summary>
/// Stores, replaces and deletes the Claude key (backend API 1.21.0). The typed value lives only in <see cref="_value"/>
/// until the save call and is dropped before the call returns; the status shown afterwards comes from the API's answer.
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

    [Inject]
    private PageOrigin Origin { get; set; } = default!;

    private ClaudeKeyStatusDto Key { get; set; } = ClaudeKeyStatusDto.NotConfigured;

    private string? Error { get; set; }

    private bool Busy { get; set; }

    /// <summary>True after "Replace key" until the new key was saved or the replace was cancelled.</summary>
    private bool Editing { get; set; }

    /// <summary>True while the delete confirmation is open.</summary>
    private bool Confirming { get; set; }

    /// <summary>True when the page was not opened under a loopback host name or the API answered <c>403 local-only</c>.</summary>
    private bool LocalOnly { get; set; }

    private bool Locked => Busy || LocalOnly;

    private bool HasStored => Key.Stored != StoredClaudeKeyStateDto.Absent;

    private bool ShowsInput => !HasStored || Editing;

    private string SaveText => HasStored ? "Save new key" : "Save key";

    private bool CanSave => !Locked && Key.StoreAvailable && !string.IsNullOrWhiteSpace(_value);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Key = Initial;
        LocalOnly = !Origin.IsLocal;
    }

    private void OnInput(ChangeEventArgs e) => _value = e.Value?.ToString() ?? string.Empty;

    private void ClearInput()
    {
        _value = string.Empty;
        _generation++;
    }

    private void Replace()
    {
        ClearInput();
        Editing = true;
        Confirming = false;
    }

    private void Cancel()
    {
        ClearInput();
        Editing = false;
    }

    private async Task SaveAsync()
    {
        var value = _value;
        ClearInput();
        await ApplyAsync(() => Api.StoreClaudeKeyAsync(value));
    }

    private async Task DeleteAsync()
    {
        Confirming = false;
        await ApplyAsync(() => Api.DeleteClaudeKeyAsync());
    }

    private async Task ApplyAsync(Func<Task<ClaudeKeyStatusDto>> call)
    {
        Busy = true;
        try
        {
            await AcceptAsync(await call());
        }
        catch (ApiException ex)
        {
            Fail(ex.Error);
        }

        Busy = false;
    }

    private async Task AcceptAsync(ClaudeKeyStatusDto key)
    {
        (Key, Error, Editing) = (key, null, false);
        await Changed.InvokeAsync(key);
    }

    private void Fail(ApiError error)
    {
        LocalOnly |= ClaudeKeyErrors.IsLocalOnly(error);
        Error = LocalOnly ? null : ClaudeKeyErrors.Message(error);
    }
}
