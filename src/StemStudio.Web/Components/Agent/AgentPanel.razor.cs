using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;

namespace StemStudio.Web.Components.Agent;

/// <summary>
/// The composer agent panel: renders <see cref="AgentPanelState"/>, loads it when shown and turns Enter in the input
/// into Send (Shift+Enter keeps a new line).
/// </summary>
public sealed partial class AgentPanel
{
    /// <summary>How often the open panel refreshes the backend status (US3, FR-023).</summary>
    public static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(10);

    private readonly CancellationTokenSource _stop = new();
    private bool _enterPressed;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    [Inject]
    private AgentPanelState State { get; set; } = default!;

    private string Input { get; set; } = string.Empty;

    private string? StreamingText => State.Run is { Ended: false, Text.Length: > 0 } run ? run.Text : null;

    /// <inheritdoc />
    public void Dispose()
    {
        State.Changed -= OnChanged;
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnChanged;
        await State.LoadAsync();
        _ = RefreshStatusLoopAsync(_stop.Token);
    }

    private async Task RefreshStatusLoopAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(StatusInterval, Time, ct);
                await InvokeAsync(State.RefreshStatusAsync);
            }
        }
        catch (OperationCanceledException)
        {
            // The panel was closed.
        }
    }

    private static string UsageText(TokenUsageDto usage) => string.Create(
        CultureInfo.InvariantCulture,
        $"Tokens: {usage.InputTokens:N0} input ({usage.CachedInputTokens:N0} cached), {usage.OutputTokens:N0} output");

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnKeyDown(KeyboardEventArgs e) => _enterPressed = e.Key == "Enter" && !e.ShiftKey;

    private async Task OnInputAsync(ChangeEventArgs e)
    {
        var value = e.Value?.ToString() ?? string.Empty;
        var enter = _enterPressed && AgentWire.TryStripEnter(Input, value, out value);
        _enterPressed = false;
        Input = value;
        if (enter)
        {
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (await State.SendAsync(Input))
        {
            Input = string.Empty;
        }
    }
}
