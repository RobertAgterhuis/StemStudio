using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Scores;

/// <summary>
/// Makes a sheet music item of one channel with the backend's default options and no tablature, and waits until it is
/// rendered (016: the MIDI action FR-023 and "Generate drum notation" of the drum trainer FR-034).
/// <see cref="ApiException"/> reaches the caller.
/// </summary>
/// <param name="api">The API client.</param>
/// <param name="timer">The polling timer.</param>
/// <param name="failedText">The reason shown when the item failed without one of its own.</param>
public sealed class ChannelScoreMaker(IStemStudioApiClient api, IPollingTimer timer, string failedText)
{
    /// <summary>Render progress 0..100 of the item being made.</summary>
    public int Progress { get; private set; }

    /// <summary>The readable reason when the item could not be made, else null.</summary>
    public string? Error { get; private set; }

    /// <summary>Creates the item and waits for it.</summary>
    /// <param name="track">The track.</param>
    /// <param name="channel">The channel id, e.g. <c>drums</c> or <c>drums.kick</c>.</param>
    /// <param name="changed">Called after every progress update.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The ready item, or null when it failed (<see cref="Error"/>) or the wait was stopped.</returns>
    public async Task<AssetDto?> CreateAsync(TrackDto track, string channel, Func<Task> changed, CancellationToken ct)
    {
        (Progress, Error) = (0, null);
        var target = ScoreTarget.ForChannel(track, channel);
        var form = new ScoreForm(await api.GetScoreOptionsAsync(target.Source, ct), target);
        if (form.Tab is { } tab)
        {
            tab.Layout = TabLayoutChoice.None;
        }

        Error = form.Validate();
        return Error is null ? await WaitAsync(await api.CreateScoreAsync(form.Request(), ct), changed, ct) : null;
    }

    private async Task<AssetDto?> WaitAsync(AssetDto item, Func<Task> changed, CancellationToken ct)
    {
        while (item.Status is AssetStatusDto.Queued or AssetStatusDto.Rendering)
        {
            if (!await timer.WaitAsync(ct))
            {
                return null;
            }

            item = await api.GetAssetAsync(item.Id, ct);
            Progress = item.Progress;
            await changed();
        }

        return item.Status == AssetStatusDto.Failed ? Failed(item) : item;
    }

    private AssetDto? Failed(AssetDto item)
    {
        Error = item.Error ?? failedText;
        return null;
    }
}
