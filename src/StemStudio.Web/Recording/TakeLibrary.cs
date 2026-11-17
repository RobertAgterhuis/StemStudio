using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Mixer;

namespace StemStudio.Web.Recording;

/// <summary>
/// The takes of the open track (newest first) and the one take loaded into the <c>take</c> strip (FR-011, FR-012).
/// A new capture plays from the local audio at once; when its server copy is ready the strip switches to the saved
/// FLAC and the capture is released.
/// </summary>
/// <param name="engine">The recording engine.</param>
/// <param name="audio">The engine wrapper (take channel and meter).</param>
/// <param name="api">The backend client.</param>
/// <param name="audioUrl">Makes a take's relative <c>audioUrl</c> absolute for the browser.</param>
public sealed partial class TakeLibrary(IRecordingEngine engine, IAudioEngine audio, IStemStudioApiClient api, Func<string, string> audioUrl)
{
    /// <summary>The channel id and test-id key of the take strip.</summary>
    public const string Channel = "take";

    private readonly List<TakeItem> _items = [];

    /// <summary>Raised after the strip changed (take loaded, saved, unloaded or a strip setting), for the mix's take channel.</summary>
    public event Action? StripChanged;

    /// <summary>The takes, newest first.</summary>
    public IReadOnlyList<TakeItem> Items => _items;

    /// <summary>The take strip's settings (volume, pan, mute, solo).</summary>
    public ChannelState Strip { get; } = new(Channel);

    /// <summary>The take in the strip, or null.</summary>
    public TakeItem? Loaded { get; private set; }

    /// <summary>Total size of the stored takes in bytes.</summary>
    public long TotalSizeBytes => _items.Sum(i => i.Take?.SizeBytes ?? 0);

    /// <summary>Number of takes that count against the limit of 200 per track.</summary>
    public int Count => _items.Count;

    /// <summary>A readable load error of the take list or strip, or null.</summary>
    public string? Error { get; private set; }

    /// <summary>Lists the stored takes of a track.</summary>
    /// <param name="trackId">The track.</param>
    /// <returns>A task that completes when listed.</returns>
    public async Task LoadAsync(Guid trackId)
    {
        var (list, error) = await ApiCall.TryAsync(() => api.ListTakesAsync(trackId));
        Error = error;
        if (list is not null)
        {
            Merge(list.Items);
        }
    }

    /// <summary>Adds the captures of a finished session and loads the last one into the strip.</summary>
    /// <param name="items">The new local items.</param>
    /// <returns>A task that completes when the strip plays the newest capture.</returns>
    public async Task AddAsync(IReadOnlyList<TakeItem> items)
    {
        _items.AddRange(items);
        Sort();
        await (items.Count > 0 ? LoadIntoStripAsync(items[^1]) : Task.CompletedTask);
    }

    /// <summary>Takes whose server copy is still being saved.</summary>
    /// <returns>The items.</returns>
    public IReadOnlyList<TakeItem> SavingOnServer() => [.. _items.Where(i => i.Take?.Status == TakeStatusDto.Saving)];

    /// <summary>Applies a polled server state; a ready take with a local capture switches the strip and frees the capture.</summary>
    /// <param name="item">The item.</param>
    /// <param name="take">The server take.</param>
    /// <returns>A task that completes when applied.</returns>
    public async Task ApplyAsync(TakeItem item, RecordingTakeDto take)
    {
        item.Apply(take);
        if (take.Status != TakeStatusDto.Ready || item.CaptureKey is not { } key)
        {
            return;
        }

        await (ReferenceEquals(Loaded, item) ? LoadSavedAsync(item) : Task.CompletedTask);
        await engine.ReleaseCaptureAsync(key);
        item.ReleaseCapture();
        StripChanged?.Invoke();
    }

    /// <summary>Loads a take into the strip (local capture or saved FLAC) and applies the strip settings.</summary>
    /// <param name="item">The take.</param>
    /// <returns>A task that completes when the take is scheduled.</returns>
    public async Task LoadIntoStripAsync(TakeItem item)
    {
        try
        {
            await engine.LoadTakeAsync(Request(item));
            Loaded = item;
            Error = null;
            await audio.SetChannelAsync(Strip.ToDto());
            StripChanged?.Invoke();
        }
        catch (Exception ex) when (ex is RecordingException or InvalidOperationException)
        {
            Error = $"The take could not be loaded: {ex.Message}";
        }
    }

    /// <summary>Binds the strip meter once the strip is rendered.</summary>
    /// <returns>A task that completes when attached.</returns>
    public Task AttachMeterAsync() => audio.AttachMeterAsync(Channel, $"meter-{Channel}");

    /// <summary>Applies a changed strip setting to the engine.</summary>
    /// <param name="change">Mutates <see cref="Strip"/>.</param>
    /// <returns>A task that completes when delivered.</returns>
    public Task ChangeStripAsync(Action<ChannelState> change)
    {
        change(Strip);
        StripChanged?.Invoke();
        return audio.SetChannelAsync(Strip.ToDto());
    }

    /// <summary>The strip settings for a saved mix (FR-012), or null when no saved take is loaded.</summary>
    /// <returns>The settings.</returns>
    public TakeChannelSettingsDto? MixSettings() => Loaded?.Take is { } take
        ? new TakeChannelSettingsDto { TakeId = take.Id, Volume = Strip.Volume, Pan = Strip.Pan, Mute = Strip.Mute, Solo = Strip.Solo }
        : null;

    private Task LoadSavedAsync(TakeItem item) => engine.LoadTakeAsync(Request(item, preferSaved: true));

    private LoadTakeRequest Request(TakeItem item, bool preferSaved = false)
    {
        var source = item.CaptureKey is { } key && !preferSaved
            ? TakeSource.FromCapture(key)
            : TakeSource.FromUrl(audioUrl(item.Take!.AudioUrl!), item.Take.StartSample, item.Take.LengthSamples, item.Take.InputChannels);
        var part = item.Take?.Part ?? item.Upload!.Part;
        return new LoadTakeRequest(item.Id, source, part, item.Take?.AdjustmentSamples ?? 0);
    }

    // Keeps the item objects of takes already shown (the strip refers to one) and drops saved takes the server no longer has.
    private void Merge(IReadOnlyList<RecordingTakeDto> takes)
    {
        var ids = takes.Select(t => t.Id).ToHashSet();
        _items.RemoveAll(i => i.Take is { } take && i.Capture is null && !ids.Contains(take.Id));
        foreach (var take in takes)
        {
            Upsert(take);
        }

        Sort();
    }

    private void Upsert(RecordingTakeDto take)
    {
        if (_items.FirstOrDefault(i => i.Take?.Id == take.Id) is { } existing)
        {
            existing.Apply(take);
            return;
        }

        _items.Add(TakeItem.Saved(take));
    }

    private void Sort() => _items.Sort((a, b) => Recorded(b).CompareTo(Recorded(a)) is var order and not 0 ? order : (b.PassIndex ?? 0).CompareTo(a.PassIndex ?? 0));

    private static DateTimeOffset Recorded(TakeItem item) => item.Take?.RecordedAt ?? item.Capture!.RecordedAt;
}
