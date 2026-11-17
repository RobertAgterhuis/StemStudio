using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.LibraryCare;

namespace StemStudio.Web.Components.Care;

/// <summary>Sends the typed <c>DISCARD</c> and reports the outcome (013 US2).</summary>
public sealed partial class DiscardPreviousDialog : CareDialog
{
    /// <summary>The previous library was deleted.</summary>
    [Parameter]
    public EventCallback OnDiscarded { get; set; }

    /// <summary>Cancel or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private string Typed { get; set; } = string.Empty;

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnClose);

    private async Task DiscardAsync()
    {
        Busy = true;
        var (_, error) = await CareCall.TryAsync(async () =>
        {
            await Client.DiscardPreviousLibraryAsync(Typed);
            return true;
        });
        Busy = false;
        ShowError(error);
        if (error is null)
        {
            await OnDiscarded.InvokeAsync();
        }
    }
}
