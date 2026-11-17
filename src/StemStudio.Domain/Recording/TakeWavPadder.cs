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
    /// <param name="capture">The validated upload header and the song sample of its first frame.</param>
    /// <param name="source">The audio data (the upload at its first audio byte, or a spool of the data).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The level facts of the copied audio.</returns>
    /// <exception cref="TakeWavException">The source ends before the declared data size.</exception>
    public static async Task<PeakScan> WriteAsync(Stream output, PaddedCapture capture, Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(capture);
        var header = capture.Header;
        await output.WriteAsync(Header(header.Channels, capture.StartSample + header.Frames), cancellationToken);
        await WriteZerosAsync(output, capture.StartSample * header.BlockAlign, cancellationToken);
        var scanner = new PeakScanner();
        await new ExactCopy(header.DataBytes, scanner).RunAsync(source, output, cancellationToken);
        return scanner.Result();
    }

    private static async Task WriteZerosAsync(Stream output, long count, CancellationToken cancellationToken)
    {
        var zeros = new byte[BufferBytes];
        for (var left = count; left > 0; left -= zeros.Length)
        {
            await output.WriteAsync(zeros.AsMemory(0, (int)Math.Min(left, zeros.Length)), cancellationToken);
        }
    }
}

/// <summary>The upload header of a capture and the song sample of its first frame (data-model 2.4).</summary>
/// <param name="Header">The validated WAV header.</param>
/// <param name="StartSample">Song sample of the first take sample (&gt;= 0).</param>
public sealed record PaddedCapture(TakeWavHeader Header, long StartSample)
{
    /// <summary>The validated start (negative starts are refused before).</summary>
    public long StartSample { get; } = StartSample >= 0 ? StartSample : throw new ArgumentOutOfRangeException(nameof(StartSample));
}

/// <summary>Copies exactly <paramref name="count"/> bytes in 64 KiB blocks, optionally scanning them; a short source is an invalid WAV.</summary>
/// <param name="count">Bytes to copy.</param>
/// <param name="scanner">Receives every copied byte, or null.</param>
public sealed class ExactCopy(long count, PeakScanner? scanner = null)
{
    private const int BufferBytes = 64 * 1024;

    /// <summary>Copies.</summary>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="TakeWavException">The source ends early.</exception>
    public async Task RunAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        var buffer = new byte[BufferBytes];
        for (var left = count; left > 0;)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(left, buffer.Length)), cancellationToken);
            if (read == 0)
            {
                throw new TakeWavException("The file ends before its audio data does.");
            }

            scanner?.Add(buffer.AsSpan(0, read));
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            left -= read;
        }
    }
}
