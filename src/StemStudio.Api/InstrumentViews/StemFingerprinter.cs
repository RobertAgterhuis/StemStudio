using System.Security.Cryptography;
using StemStudio.Api.Storage;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Rendering;

namespace StemStudio.Api.InstrumentViews;

/// <summary>
/// The source fingerprint of an analysis (data-model 7.1): SHA-256 over the sorted <c>(channel, sha256)</c> of the input
/// stem files in local storage. A missing file counts as <c>missing</c>, so its later arrival changes the fingerprint.
/// Paths come only from the stores (contained in the data directory), never from a request.
/// </summary>
/// <param name="stems">Standard stem files.</param>
/// <param name="details">Detail stem files.</param>
public sealed class StemFingerprinter(IStemFileStore stems, IDetailStemFileStore details)
{
    /// <summary>Hash text of a missing file.</summary>
    public const string Missing = "missing";

    /// <summary>The fingerprint of the given input channels of a track.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="inputs">The input channels.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Lower-case hex SHA-256.</returns>
    public async Task<string> FingerprintAsync(Guid trackId, IReadOnlyList<RenderChannel> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var hashed = new List<AnalysisInput>(inputs.Count);
        foreach (var input in inputs)
        {
            hashed.Add(new AnalysisInput(input.Id, await HashAsync(File(trackId, input), cancellationToken)));
        }

        return AnalysisRules.Fingerprint(hashed);
    }

    private FileInfo? File(Guid trackId, RenderChannel input) =>
        input.Detail is null ? stems.FindStem(trackId, input.Parent) : details.Find(new DetailStemKey(trackId, input.Parent, input.Detail));

    private static async Task<string> HashAsync(FileInfo? file, CancellationToken cancellationToken)
    {
        if (file is not { Exists: true })
        {
            return Missing;
        }

        await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}
