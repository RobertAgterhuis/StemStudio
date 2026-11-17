using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Web.LibraryCare;

/// <summary>Texts and ids of the clean-up (013 US4, FR-022-FR-024; ui-test-ids 1.12.0 cleanup-*).</summary>
public static class CleanupText
{
    /// <summary>The dialog title of a clean-up request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>For example "Remove detail stems".</returns>
    public static string Title(CleanupRequestDto request) => request.Actions[0].Kind switch
    {
        CleanupKindDto.DetailStems => "Remove detail stems",
        CleanupKindDto.WorkerCache => "Clear worker cache",
        _ => "Remove leftovers",
    };

    /// <summary>The id part of a plan row: <c>&lt;kind&gt;-&lt;trackId&gt;</c> or <c>&lt;kind&gt;-all</c>.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="trackId">The track, or null for worker-wide items.</param>
    /// <returns>The id part.</returns>
    public static string ItemId(CleanupKindDto kind, Guid? trackId) => $"{CareText.Kebab(kind)}-{trackId?.ToString() ?? "all"}";

    /// <summary>The name of an item: its track title, or what it is.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="title">The track title, or null.</param>
    /// <returns>The name.</returns>
    public static string Name(CleanupKindDto kind, string? title) => title ?? kind switch
    {
        CleanupKindDto.DetailStems => "Detail stems",
        CleanupKindDto.WorkerCache => "Worker cache",
        _ => "Leftovers",
    };

    /// <summary>Why an item is skipped.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The text.</returns>
    public static string Reason(CleanupSkipReasonDto reason) => reason switch
    {
        CleanupSkipReasonDto.DetailActive => "a split is running for this track",
        CleanupSkipReasonDto.JobActive => "a job uses it",
        CleanupSkipReasonDto.ExportActive => "an export uses it",
        CleanupSkipReasonDto.WorkerUnavailable => "the worker is not available",
        _ => "there is nothing to remove",
    };

    /// <summary>The request for one kind, optionally for one track.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="trackId">The track, or null for all.</param>
    /// <returns>The request.</returns>
    public static CleanupRequestDto Request(CleanupKindDto kind, Guid? trackId = null) =>
        new() { Actions = [new CleanupActionDto { Kind = kind, TrackIds = trackId is { } id ? [id] : null }] };

    /// <summary>Why the clean-up buttons are off: during a backup or a restore (FR-024), else null.</summary>
    /// <param name="status">The library care status.</param>
    /// <returns>The reason, or null.</returns>
    public static string? Blocked(LibraryCareStatusDto? status) =>
        status?.Gate != GateModeDto.Open || status.ActiveJob?.Kind is LibraryJobKindDto.Restore or LibraryJobKindDto.UndoRestore
            ? "Clean-up waits until the running backup or restore has finished."
            : null;
}
