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

    /// <summary>Shows a modal confirmation whose body carries a <c>data-testid</c> (e.g. <c>chords-overwrite-confirm</c>, 016).</summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">The question.</param>
    /// <param name="okText">Text of the confirming button.</param>
    /// <param name="testId">Test id of the dialog body.</param>
    /// <returns>True when the user confirmed.</returns>
    Task<bool> ConfirmAsync(string title, string message, string okText, string testId);
}

/// <summary><see cref="IConfirmService"/> backed by a Radzen dialog rendering <see cref="ConfirmDialog"/>.</summary>
/// <param name="dialogs">Radzen's dialog service.</param>
public sealed class DialogConfirmService(DialogService dialogs) : IConfirmService
{
    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string okText = "OK") => OpenAsync(title, message, okText, null);

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string okText, string testId) => OpenAsync(title, message, okText, testId);

    private async Task<bool> OpenAsync(string title, string message, string okText, string? testId)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ConfirmDialog.Message)] = message,
            [nameof(ConfirmDialog.OkText)] = okText,
            [nameof(ConfirmDialog.TestId)] = testId,
        };
        object? result = await dialogs.OpenAsync<ConfirmDialog>(title, parameters, new DialogOptions { CloseDialogOnEsc = true });
        return result is true;
    }
}
