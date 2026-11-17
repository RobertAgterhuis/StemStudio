using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.LibraryCare;

namespace StemStudio.Web.Components.Care;

/// <summary>Previews a clean-up, executes the plan after the confirmation and reports what was freed (013 US4).</summary>
public sealed partial class CleanupDialog : CareDialog
{
    /// <summary>What to clean up.</summary>
    [Parameter, EditorRequired]
    public CleanupRequestDto Request { get; set; } = default!;

    /// <summary>Cancel, Close or Escape; true when something was removed (the host measures again).</summary>
    [Parameter]
    public EventCallback<bool> OnClose { get; set; }

    private CleanupPlanDto? Plan { get; set; }

    private CleanupResultDto? Result { get; set; }

    private bool CanConfirm => Plan is { Items.Count: > 0 } && Error is null && !Busy;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await PreviewAsync();
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => args.Key == "Escape" ? CloseAsync() : Task.CompletedTask;

    private Task CloseAsync() => OnClose.InvokeAsync(Result is not null);

    private async Task PreviewAsync()
    {
        ShowError(null);
        Plan = null;
        var (plan, error) = await CareCall.TryAsync(() => Client.PreviewCleanupAsync(Request));
        Plan = plan;
        ShowError(error);
    }

    private async Task ConfirmAsync()
    {
        Busy = true;
        var planId = Plan!.PlanId;
        var (result, error) = await CareCall.TryAsync(() => Client.ExecuteCleanupAsync(planId));
        Busy = false;
        Result = result;
        ShowError(error);
    }
}
