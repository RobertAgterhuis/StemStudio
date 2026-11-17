using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>Cancels the active assists of a sheet before it is deleted.</summary>
/// <param name="assists">Assists.</param>
/// <param name="clock">Clock.</param>
public sealed class LyricAssistCanceller(ILyricAssistRepository assists, TimeProvider clock) : ILyricAssistCanceller
{
    /// <inheritdoc />
    public async Task CancelForSheetAsync(Guid sheetId, CancellationToken cancellationToken)
    {
        foreach (var assist in (await assists.ActiveAsync(cancellationToken)).Where(a => a.SheetId == sheetId))
        {
            assist.Cancel(clock.GetUtcNow().UtcDateTime);
        }

        await assists.SaveAsync(cancellationToken);
    }
}
