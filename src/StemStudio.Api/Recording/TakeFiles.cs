using System.Globalization;
using StemStudio.Api.Storage;
using StemStudio.Domain.Assets;

namespace StemStudio.Api.Recording;

/// <summary>
/// The files of a take under <c>assets/&lt;assetId&gt;/</c> (data-model 2.2, 2.4): the padded <c>capture.wav</c> the render
/// reads and the transient spool of an upload. Every path is built from the GUID and a constant name and checked for
/// containment in the data directory; nothing of the request (file names, part names) reaches a path.
/// </summary>
/// <param name="data">The data directory.</param>
public sealed class TakeFiles(DataDirectory data)
{
    /// <summary>The spool of an upload (ignored by the audio-hash scan as <c>.tmp</c>).</summary>
    public const string SpoolSuffix = ".upload.tmp";

    /// <summary>The capture relative to the data directory (the worker render input).</summary>
    /// <param name="assetId">The take's asset.</param>
    /// <returns>The path with forward slashes.</returns>
    public static string CaptureInput(Guid assetId) => string.Create(CultureInfo.InvariantCulture, $"assets/{assetId:D}/{RecordingSource.CaptureFile}");

    /// <summary>The full path of <c>capture.wav</c>.</summary>
    /// <param name="assetId">The take's asset.</param>
    /// <returns>The path.</returns>
    public string CapturePath(Guid assetId) => data.RequireContainedPath(CaptureInput(assetId));

    /// <summary>A new spool file path for an upload into the asset's folder (the folder is created).</summary>
    /// <param name="assetId">The take's asset.</param>
    /// <returns>The path.</returns>
    public string NewSpoolPath(Guid assetId)
    {
        var path = data.RequireContainedPath(string.Create(CultureInfo.InvariantCulture, $"assets/{assetId:D}/{Guid.NewGuid():N}{SpoolSuffix}"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    /// <summary>True when the capture is stored.</summary>
    /// <param name="assetId">The take's asset.</param>
    /// <returns>Whether <c>capture.wav</c> exists.</returns>
    public bool HasCapture(Guid assetId) => File.Exists(CapturePath(assetId));

    /// <summary>Deletes <c>capture.wav</c> if present.</summary>
    /// <param name="assetId">The take's asset.</param>
    public void DeleteCapture(Guid assetId) => File.Delete(CapturePath(assetId));

    /// <summary>Deletes a file if present (spools); errors are ignored.</summary>
    /// <param name="path">A path from this store.</param>
    public static void TryDelete(string? path)
    {
        try
        {
            if (path is not null)
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A spool that cannot be deleted now is removed with its folder.
        }
    }
}
