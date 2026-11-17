namespace StemStudio.Api.LyricAssist;

/// <summary>Stops the running assist of a sheet that is being deleted (DELTA.md 1 "Compatibility").</summary>
public interface ILyricAssistCanceller
{
    /// <summary>Cancels every queued or running assist of the sheet (status cancelled, run stopped).</summary>
    /// <param name="sheetId">The sheet.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task CancelForSheetAsync(Guid sheetId, CancellationToken cancellationToken);
}
