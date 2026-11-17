using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;

namespace StemStudio.Web.Recording;

/// <summary>Rename, delete and retry of <see cref="TakeLibrary"/> (FR-014, US5).</summary>
public sealed partial class TakeLibrary
{
    /// <summary>Renames a saved take (1-80 characters after trimming).</summary>
    /// <param name="item">The take.</param>
    /// <param name="name">The new name.</param>
    /// <returns>Null when renamed, else the readable reason.</returns>
    public async Task<string?> RenameAsync(TakeItem item, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > RecordingLimits.MaxTakeNameLength)
        {
            return $"A name has 1 to {RecordingLimits.MaxTakeNameLength} characters.";
        }

        var (updated, error) = await ApiCall.TryAsync(() => api.UpdateTakeAsync(item.Take!.Id, new UpdateTakeRequest { Name = trimmed }));
        item.Apply(updated ?? item.Take!);
        return error;
    }

    /// <summary>Deletes a saved take and its files (a take in the strip is unloaded).</summary>
    /// <param name="item">The take.</param>
    /// <returns>A task that completes when deleted.</returns>
    public async Task DeleteAsync(TakeItem item)
    {
        Error = await ApiCall.RunAsync(() => api.DeleteTakeAsync(item.Take!.Id));
        if (Error is null)
        {
            await RemoveAsync([item]);
        }
    }

    /// <summary>Converts a failed take again.</summary>
    /// <param name="item">The take.</param>
    /// <returns>A task that completes when the server accepted it.</returns>
    public async Task RetryAsync(TakeItem item)
    {
        var (retried, error) = await ApiCall.TryAsync(() => api.RetryTakeAsync(item.Take!.Id));
        Error = error;
        if (retried is not null)
        {
            item.Apply(retried);
        }
    }
}
