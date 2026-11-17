using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>Cancels the active assists of a sheet before it is deleted (the run is stopped and waited for).</summary>
/// <param name="assists">Assists.</param>
/// <param name="runner">Background runner.</param>
/// <param name="clock">Clock.</param>
public sealed class LyricAssistCanceller(ILyricAssistRepository assists, LyricAssistRunner runner, TimeProvider clock) : ILyricAssistCanceller
{
    /// <inheritdoc />
    public async Task CancelForSheetAsync(Guid sheetId, CancellationToken cancellationToken)
    {
        foreach (var assist in (await assists.ActiveAsync(cancellationToken)).Where(a => a.SheetId == sheetId))
        {
            if (!await runner.CancelAsync(assist.Id))
            {
                assist.Cancel(clock.GetUtcNow().UtcDateTime);
            }
        }

        await assists.SaveAsync(cancellationToken);
    }
}
