using Radzen;
using StemStudio.Web.Components.Shared;

namespace StemStudio.Web.Services;

/// <summary>Asks the user to confirm an action (never <c>window.confirm</c>).</summary>
public interface IConfirmService
{
    /// <summary>Shows a modal confirmation.</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">The question.</param>
    /// <param name="okText">Text of the confirming button.</param>
    /// <returns>True when the user confirmed.</returns>
    Task<bool> ConfirmAsync(string title, string message, string okText = "OK");
}

/// <summary><see cref="IConfirmService"/> backed by a Radzen dialog rendering <see cref="ConfirmDialog"/>.</summary>
/// <param name="dialogs">Radzen's dialog service.</param>
public sealed class DialogConfirmService(DialogService dialogs) : IConfirmService
{
    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string okText = "OK")
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ConfirmDialog.Message)] = message,
            [nameof(ConfirmDialog.OkText)] = okText,
        };
        object? result = await dialogs.OpenAsync<ConfirmDialog>(title, parameters, new DialogOptions { CloseDialogOnEsc = true });
        return result is true;
    }
}
