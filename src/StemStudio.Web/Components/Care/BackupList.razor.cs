using Microsoft.AspNetCore.Components;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.LibraryCare;

namespace StemStudio.Web.Components.Care;

/// <summary>Row facts and download links of the archive list.</summary>
public sealed partial class BackupList
{
    /// <summary>The archives, newest first.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<BackupSummaryDto> Archives { get; set; } = [];

    /// <summary>The folder hint for the empty state ("./backups").</summary>
    [Parameter]
    public string? FolderHint { get; set; }

    /// <summary>A library job is queued or running (restore and check wait).</summary>
    [Parameter]
    public bool JobRunning { get; set; }

    /// <summary>Details pressed.</summary>
    [Parameter]
    public EventCallback<BackupSummaryDto> OnDetails { get; set; }

    /// <summary>Check pressed.</summary>
    [Parameter]
    public EventCallback<BackupSummaryDto> OnVerify { get; set; }

    /// <summary>Restore pressed.</summary>
    [Parameter]
    public EventCallback<BackupSummaryDto> OnRestore { get; set; }

    /// <summary>Delete pressed (the host asks for confirmation).</summary>
    [Parameter]
    public EventCallback<BackupSummaryDto> OnDelete { get; set; }

    [Inject]
    private Microsoft.Extensions.Options.IOptions<StemStudioWebOptions> WebOptions { get; set; } = default!;

    private string EmptyText => $"No backups yet. Make one with New backup, or copy an archive into {FolderHint ?? "the backups folder"} to restore it.";

    private static string RowFacts(BackupSummaryDto archive)
    {
        var made = archive.CreatedAt is { } created ? $"Made {CareText.When(created)}" : $"Changed {CareText.When(archive.ModifiedAt)}";
        return archive.Counts is { } counts ? $"{made} - {CareText.Counts(counts)}" : made;
    }

    private string DownloadUrl(string fileName) => new Uri(WebOptions.Value.PublicApiBaseUrl, ApiRoutes.BackupFile(fileName)).ToString();
}
