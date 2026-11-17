using Microsoft.EntityFrameworkCore;
using StemStudio.Api.Agent.Stores;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Styles;
using StemStudio.Persistence;

namespace StemStudio.Api.LibraryCare;

/// <summary>What the database says about the library for a restore (research R-07): emptiness and active work.</summary>
/// <param name="context">The live database.</param>
public sealed class LibraryState(StemStudioDbContext context)
{
    /// <summary>True when no track, mix, item, generation, lyrics sheet, voice profile, user style or conversation exists.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when empty.</returns>
    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken) =>
        !await context.Tracks.AnyAsync(cancellationToken)
        && !await context.Mixes.AnyAsync(cancellationToken)
        && !await context.Assets.AnyAsync(cancellationToken)
        && !await context.Generations.AnyAsync(cancellationToken)
        && !await context.LyricsSheets.AnyAsync(cancellationToken)
        && !await context.VoiceProfiles.AnyAsync(cancellationToken)
        && !await context.AgentConversations.AnyAsync(cancellationToken)
        && !(await context.Styles.AsNoTracking().ToListAsync(cancellationToken)).Any(style => style.Kind == StyleKind.User);

    /// <summary>Queued or running jobs that block a restore.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The work, imports first.</returns>
    public async Task<IReadOnlyList<ActiveWorkDto>> ActiveWorkAsync(CancellationToken cancellationToken)
    {
        var tracks = await context.Tracks.AsNoTracking().Include(track => track.Lyrics).ToListAsync(cancellationToken);
        var assets = await context.Assets.AsNoTracking().ToListAsync(cancellationToken);
        var generations = await context.Generations.AsNoTracking().ToListAsync(cancellationToken);
        var runs = await EfRunStore.ActiveRuns(context).AsNoTracking().Select(run => run.Id).ToListAsync(cancellationToken);
        return
        [
            .. tracks.Where(track => !track.IsTerminal).Select(track => Work(ActiveWorkKindDto.Import, track.Id, track.Title)),
            .. tracks.Where(track => track.IsDetailActive).Select(track => Work(ActiveWorkKindDto.Detail, track.Id, track.Title + " - detail")),
            .. tracks.Where(track => track.Lyrics?.Status is LyricsStatus.Aligning or LyricsStatus.AlignPending).Select(track => Work(ActiveWorkKindDto.LyricsAlignment, track.Id, track.Title + " - lyrics")),
            .. assets.Where(asset => asset.IsActive).Select(asset => Work(asset.Kind == AssetKind.Score ? ActiveWorkKindDto.Score : ActiveWorkKindDto.Export, asset.Id, asset.Name)),
            .. generations.Where(generation => generation.IsActive).Select(generation => Work(ActiveWorkKindDto.Generation, generation.Id, "Generation")),
            .. runs.Select(run => Work(ActiveWorkKindDto.AgentRun, run, "Agent run")),
        ];
    }

    private static ActiveWorkDto Work(ActiveWorkKindDto kind, Guid id, string title) => new()
    {
        Kind = kind,
        Id = id.ToString("D", System.Globalization.CultureInfo.InvariantCulture),
        Title = title.Length > 300 ? title[..300] : title,
    };
}
