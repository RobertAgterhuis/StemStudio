using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;
using StemStudio.Web.LibraryCare;

namespace StemStudio.Web.Components.Care;

/// <summary>Loads the details of one archive when its file name changes and moves the focus to the panel title.</summary>
public sealed partial class BackupDetailsPanel
{
    private ElementReference _title;
    private string? _loaded;

    /// <summary>The archive.</summary>
    [Parameter, EditorRequired]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Close pressed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    [Inject]
    private ILibraryCareClient Client { get; set; } = default!;

    private BackupDetailsDto? Details { get; set; }

    private string? Error { get; set; }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (_loaded != FileName)
        {
            (_loaded, Details) = (FileName, null);
            (Details, Error) = await ApiCall.TryAsync(() => Client.GetBackupAsync(FileName));
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _title.FocusAsync();
        }
    }

    private static string Join(IEnumerable<BackupCategoryDto> categories) => string.Join(", ", categories.Select(CareText.Category));

    private static string ProofText(VoiceProfileProofDto proof)
    {
        var consent = $"{proof.DisplayName}: agreed by {proof.PersonName} on {proof.ConsentedAt.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} (statement {proof.StatementVersion})";
        return proof.Valid ? consent : $"{consent} - not valid: {proof.Reason ?? "the consent record does not match"}; it will not be restored";
    }
}
