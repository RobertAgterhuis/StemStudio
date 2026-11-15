using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Web.Interop;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Library;

/// <summary>Validation, focus and keys of the rename dialog; raises the trimmed, valid name.</summary>
public sealed partial class AssetRenameDialog : IAsyncDisposable
{
    private string _name = string.Empty;
    private string? _problem;
    private ElementReference _input;
    private IAsyncDisposable? _returnFocus;

    [Inject]
    private IWebInterop Web { get; set; } = default!;

    /// <summary>The current name (prefilled).</summary>
    [Parameter]
    public string Name { get; set; } = string.Empty;

    /// <summary>Why the last rename was refused.</summary>
    [Parameter]
    public string? Error { get; set; }

    /// <summary>Raised with the trimmed name.</summary>
    [Parameter]
    public EventCallback<string> OnConfirm { get; set; }

    /// <summary>Cancel pressed (button or Escape).</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _returnFocus?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _name = Name;
        _returnFocus = await Web.CaptureFocusAsync();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => firstRender ? _input.FocusAsync().AsTask() : Task.CompletedTask;

    private Task OnDialogKeyAsync(KeyboardEventArgs args) => args.Key == "Escape" ? OnCancel.InvokeAsync() : Task.CompletedTask;

    private Task OnFieldKeyAsync(KeyboardEventArgs args) => args.Key == "Enter" ? ConfirmAsync() : Task.CompletedTask;

    private Task ConfirmAsync()
    {
        _problem = AssetFilter.ValidateName(_name);
        return _problem is null ? OnConfirm.InvokeAsync(_name.Trim()) : Task.CompletedTask;
    }
}
