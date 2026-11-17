using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Recording;

namespace StemStudio.Persistence.Repositories;

/// <summary>Persistence of takes (feature 015).</summary>
public interface IRecordingTakeRepository
{
    /// <summary>One take, tracked.</summary>
    /// <param name="id">Take id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The take or null.</returns>
    Task<RecordingTake?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The take holding an asset, tracked.</summary>
    /// <param name="assetId">Asset id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The take or null.</returns>
    Task<RecordingTake?> FindByAssetAsync(Guid assetId, CancellationToken cancellationToken);

    /// <summary>Every take of a track, newest first, tracked.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The takes.</returns>
    Task<IReadOnlyList<RecordingTake>> ListByTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>The take counts of tracks (tracks without takes are absent).</summary>
    /// <param name="trackIds">Track ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Count per track.</returns>
    Task<IReadOnlyDictionary<Guid, int>> CountsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken);

    /// <summary>The ids of the takes of a track.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The ids.</returns>
    Task<IReadOnlySet<Guid>> IdsOfTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>The mixes of a track whose take channel shows one of the takes, tracked.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="takeIds">Take ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The mixes.</returns>
    Task<IReadOnlyList<Mix>> MixesShowingAsync(Guid trackId, IReadOnlyCollection<Guid> takeIds, CancellationToken cancellationToken);

    /// <summary>Adds a take; saved with the next save of the scope.</summary>
    /// <param name="take">New take.</param>
    void Add(RecordingTake take);

    /// <summary>Removes a take; saved with the next save of the scope.</summary>
    /// <param name="take">Tracked take.</param>
    void Remove(RecordingTake take);

    /// <summary>Saves the changes of the scope.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">A take number was taken concurrently.</exception>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>Persistence of input-latency calibrations (feature 015).</summary>
public interface IInputCalibrationRepository
{
    /// <summary>Every calibration by label.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The calibrations.</returns>
    Task<IReadOnlyList<InputCalibration>> ListAsync(CancellationToken cancellationToken);

    /// <summary>One calibration, tracked.</summary>
    /// <param name="id">Calibration id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The calibration or null.</returns>
    Task<InputCalibration?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The calibration of a trimmed label (ordinal), tracked.</summary>
    /// <param name="deviceLabel">Trimmed label.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The calibration or null.</returns>
    Task<InputCalibration?> FindByLabelAsync(string deviceLabel, CancellationToken cancellationToken);

    /// <summary>Adds a calibration; saved with the next save.</summary>
    /// <param name="calibration">New calibration.</param>
    void Add(InputCalibration calibration);

    /// <summary>Removes a calibration and detaches its takes (their latency stays); saved with the next save.</summary>
    /// <param name="calibration">Tracked calibration.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(InputCalibration calibration, CancellationToken cancellationToken);

    /// <summary>Saves the changes of the scope.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">The label was stored concurrently.</exception>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IRecordingTakeRepository"/> and <see cref="IInputCalibrationRepository"/>.</summary>
/// <param name="context">The scoped context.</param>
public sealed class RecordingRepository(StemStudioDbContext context) : IRecordingTakeRepository, IInputCalibrationRepository
{
    /// <inheritdoc />
    public Task<RecordingTake?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.RecordingTakes.SingleOrDefaultAsync(take => take.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<RecordingTake?> FindByAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        context.RecordingTakes.SingleOrDefaultAsync(take => take.AssetId == assetId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecordingTake>> ListByTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        await context.RecordingTakes.Where(take => take.TrackId == trackId)
            .OrderByDescending(take => take.CreatedAt).ThenByDescending(take => take.Number).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken) =>
        await context.RecordingTakes.Where(take => trackIds.Contains(take.TrackId)).GroupBy(take => take.TrackId)
            .Select(group => new { group.Key, Count = group.Count() }).ToDictionaryAsync(entry => entry.Key, entry => entry.Count, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> IdsOfTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        (await context.RecordingTakes.Where(take => take.TrackId == trackId).Select(take => take.Id).ToListAsync(cancellationToken)).ToHashSet();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Mix>> MixesShowingAsync(Guid trackId, IReadOnlyCollection<Guid> takeIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(takeIds);
        var mixes = await context.Mixes.Where(mix => mix.TrackId == trackId).ToListAsync(cancellationToken);
        return mixes.Where(mix => mix.Take is { } take && takeIds.Contains(take.TakeId)).ToList();
    }

    /// <inheritdoc />
    public void Add(RecordingTake take) => context.RecordingTakes.Add(take);

    /// <inheritdoc />
    public void Remove(RecordingTake take) => context.RecordingTakes.Remove(take);

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);

    /// <inheritdoc />
    async Task<IReadOnlyList<InputCalibration>> IInputCalibrationRepository.ListAsync(CancellationToken cancellationToken) =>
        await context.InputCalibrations.AsNoTracking().OrderBy(calibration => calibration.DeviceLabel).ToListAsync(cancellationToken);

    /// <inheritdoc />
    Task<InputCalibration?> IInputCalibrationRepository.FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.InputCalibrations.SingleOrDefaultAsync(calibration => calibration.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<InputCalibration?> FindByLabelAsync(string deviceLabel, CancellationToken cancellationToken) =>
        context.InputCalibrations.SingleOrDefaultAsync(calibration => calibration.DeviceLabel == deviceLabel, cancellationToken);

    /// <inheritdoc />
    public void Add(InputCalibration calibration) => context.InputCalibrations.Add(calibration);

    /// <inheritdoc />
    public async Task RemoveAsync(InputCalibration calibration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        foreach (var take in await context.RecordingTakes.Where(take => take.CalibrationId == calibration.Id).ToListAsync(cancellationToken))
        {
            take.DetachCalibration();
        }

        context.InputCalibrations.Remove(calibration);
    }
}
