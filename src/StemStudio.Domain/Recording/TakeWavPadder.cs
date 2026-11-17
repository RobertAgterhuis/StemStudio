using System.Buffers.Binary;

namespace StemStudio.Domain.Recording;

/// <summary>
/// Writes <c>capture.wav</c> (data-model 2.4): a canonical 44-byte PCM header for <c>startSample + frames</c> frames,
/// <c>startSample x blockAlign</c> zero bytes, then exactly the uploaded audio data, streamed (never buffered whole).
/// </summary>
public static class TakeWavPadder
{
    /// <summary>Size of the canonical header.</summary>
    public const int HeaderBytes = 44;

    private const int BufferBytes = 64 * 1024;

    /// <summary>The canonical header of a 24-bit 44.1 kHz PCM file.</summary>
    /// <param name="channels">1 or 2.</param>
    /// <param name="frames">Frames of the whole file.</param>
    /// <returns>The 44 header bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The file would not fit a RIFF size.</exception>
    public static byte[] Header(int channels, long frames)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, 2);
        var blockAlign = 3 * channels;
        var dataBytes = frames * blockAlign;
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dataBytes, uint.MaxValue - 36L, nameof(frames));
        var header = new byte[HeaderBytes];
        var span = header.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], (uint)(36 + dataBytes));
        "WAVEfmt "u8.CopyTo(span[8..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[22..], (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], RecordingConstants.SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], (uint)(RecordingConstants.SampleRate * blockAlign));
        BinaryPrimitives.WriteUInt16LittleEndian(span[32..], (ushort)blockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(span[34..], RecordingConstants.BitDepth);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[40..], (uint)dataBytes);
        return header;
    }

    /// <summary>Writes the padded file and scans the copied audio.</summary>
    /// <param name="output">The destination.</param>
    /// <param name="header">The validated upload header (the source stream is at its first audio byte).</param>
    /// <param name="startSample">Song sample of the first take sample (zero frames before it).</param>
    /// <param name="source">The upload, positioned at the audio data.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The level facts of the copied audio.</returns>
    /// <exception cref="TakeWavException">The upload ends before its declared data size.</exception>
    public static async Task<PeakScan> WriteAsync(Stream output, TakeWavHeader header, long startSample, Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(startSample);
        await output.WriteAsync(Header(header.Channels, startSample + header.Frames), cancellationToken);
        var buffer = new byte[BufferBytes];
        await WriteZerosAsync(output, buffer, startSample * header.BlockAlign, cancellationToken);
        return await CopyAsync(output, buffer, source, header.DataBytes, cancellationToken);
    }

    private static async Task WriteZerosAsync(Stream output, byte[] buffer, long count, CancellationToken cancellationToken)
    {
        for (var left = count; left > 0; left -= buffer.Length)
        {
            await output.WriteAsync(buffer.AsMemory(0, (int)Math.Min(left, buffer.Length)), cancellationToken);
        }
    }

    private static async Task<PeakScan> CopyAsync(Stream output, byte[] buffer, Stream source, long count, CancellationToken cancellationToken)
    {
        var scanner = new PeakScanner();
        for (var left = count; left > 0;)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(left, buffer.Length)), cancellationToken);
            if (read == 0)
            {
                throw new TakeWavException("The file ends before its audio data does.");
            }

            scanner.Add(buffer.AsSpan(0, read));
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            left -= read;
        }

        return scanner.Result();
    }
}
