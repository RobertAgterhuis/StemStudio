using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.BandInfo;

/// <summary>Validation, focus and keys of the correction dialog (feature 005, FR-009).</summary>
public sealed partial class CorrectMetadataDialog
{
    private string _title = string.Empty;
    private string _artist = string.Empty;
    private string? _problem;

    /// <summary>The current title (prefilled).</summary>
    [Parameter]
    public string Title { get; set; } = string.Empty;

    /// <summary>The current artist (prefilled; empty when unknown).</summary>
    [Parameter]
    public string? Artist { get; set; }

    /// <summary>Why the last request was refused (409, 422).</summary>
    [Parameter]
    public string? Error { get; set; }

    /// <summary>True while the request runs.</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Raised with the trimmed, valid names.</summary>
    [Parameter]
    public EventCallback<MetadataLookupRequest> OnSubmit { get; set; }

    /// <summary>Cancel pressed (button or Escape).</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        (_title, _artist) = (Title, Artist ?? string.Empty);
        await base.OnInitializedAsync();
    }

    private Task OnDialogKeyAsync(KeyboardEventArgs args) => args.Key == "Escape" ? OnCancel.InvokeAsync() : Task.CompletedTask;

    private Task OnFieldKeyAsync(KeyboardEventArgs args) => args.Key == "Enter" && !Busy ? SubmitAsync() : Task.CompletedTask;

    private Task SubmitAsync()
    {
        _problem = MetadataNames.Validate(_title, _artist);
        return _problem is null ? OnSubmit.InvokeAsync(MetadataNames.Request(_title, _artist)) : Task.CompletedTask;
    }
}
