using System.Buffers;
using System.Security.Cryptography;

namespace StemStudio.Api.Storage;

/// <summary>SHA-256 and size of copied content.</summary>
/// <param name="Sha256">Lower-case hex SHA-256.</param>
/// <param name="SizeBytes">Number of bytes.</param>
public sealed record HashResult(string Sha256, long SizeBytes);

/// <summary>Streams content to a destination while hashing it and enforcing a size limit (never buffers the whole file).</summary>
public static class HashingCopy
{
    private const int BufferSize = 81920;

    /// <summary>Copies <paramref name="source"/> to <paramref name="destination"/>.</summary>
    /// <param name="source">Source stream (e.g. a multipart section body).</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="maxBytes">Largest accepted size; one byte more fails.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Hash and size.</returns>
    /// <exception cref="UploadTooLargeException">More than <paramref name="maxBytes"/> bytes were read.</exception>
    public static async Task<HashResult> CopyAsync(Stream source, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            var total = await PumpAsync(source, destination, (hash, buffer, maxBytes), cancellationToken);
            return new HashResult(Convert.ToHexStringLower(hash.GetHashAndReset()), total);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<long> PumpAsync(
        Stream source, Stream destination, (IncrementalHash Hash, byte[] Buffer, long MaxBytes) state, CancellationToken cancellationToken)
    {
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(state.Buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
        {
            total += read;
            if (total > state.MaxBytes)
            {
                throw new UploadTooLargeException();
            }

            state.Hash.AppendData(state.Buffer, 0, read);
            await destination.WriteAsync(state.Buffer.AsMemory(0, read), cancellationToken);
        }

        return total;
    }
}
