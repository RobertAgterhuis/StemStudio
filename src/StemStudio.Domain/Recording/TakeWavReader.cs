using System.Buffers.Binary;
using System.Globalization;

namespace StemStudio.Domain.Recording;

/// <summary>The format of an accepted take WAV, read up to the start of its <c>data</c> chunk.</summary>
/// <param name="Channels">1 or 2.</param>
/// <param name="DataBytes">Size of the audio data (a multiple of <see cref="BlockAlign"/>).</param>
public sealed record TakeWavHeader(int Channels, long DataBytes)
{
    /// <summary>Bytes per frame (3 x channels).</summary>
    public int BlockAlign => 3 * Channels;

    /// <summary>Frames in the data chunk.</summary>
    public long Frames => DataBytes / BlockAlign;
}

/// <summary>
/// Validates an uploaded take WAV from a forward-only stream (data-model 2.3): RIFF/WAVE, first <c>fmt </c> chunk PCM or
/// <c>WAVE_FORMAT_EXTENSIBLE</c> with PCM subformat, 1-2 channels, 44,100 Hz, 24 bits; chunks before <c>data</c> are
/// skipped (at most 64 KiB in total). Every size is checked before it is used, so a hostile header cannot make the reader
/// allocate or skip more than the limits.
/// </summary>
public static class TakeWavReader
{
    /// <summary>Most bytes of chunks before <c>data</c> (including <c>fmt </c>).</summary>
    public const int MaxHeaderChunkBytes = 64 * 1024;

    private const ushort FormatPcm = 1;
    private const ushort FormatExtensible = 0xFFFE;
    private const int ExtensibleFmtSize = 40;

    /// <summary>The PCM subformat GUID bytes after the format code (KSDATAFORMAT_SUBTYPE_PCM).</summary>
    private static ReadOnlySpan<byte> PcmSubformatTail => [0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71];

    /// <summary>Reads and validates the header; the stream is left at the first audio byte.</summary>
    /// <param name="stream">The upload (read forward only).</param>
    /// <param name="maxDataBytes">Largest accepted audio data.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The format.</returns>
    /// <exception cref="TakeWavException">The file is refused.</exception>
    public static async Task<TakeWavHeader> ReadHeaderAsync(Stream stream, long maxDataBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var riff = await ReadExactAsync(stream, 12, cancellationToken);
        if (!Is(riff, 0, "RIFF") || !Is(riff, 8, "WAVE"))
        {
            throw Invalid("The file is not a RIFF/WAVE file.");
        }

        return await ReadChunksAsync(stream, maxDataBytes, cancellationToken);
    }

    private static async Task<TakeWavHeader> ReadChunksAsync(Stream stream, long maxDataBytes, CancellationToken cancellationToken)
    {
        int? channels = null;
        long headerBytes = 0;
        while (true)
        {
            var chunk = await ReadExactAsync(stream, 8, cancellationToken);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4));
            if (Is(chunk, 0, "data"))
            {
                return Data(channels ?? throw Invalid("The data chunk comes before the fmt chunk."), size, maxDataBytes);
            }

            headerBytes += 8 + size + (size & 1);
            if (headerBytes > MaxHeaderChunkBytes)
            {
                throw Invalid("The chunks before the audio data are larger than 64 KiB.");
            }

            var body = await ReadExactAsync(stream, (int)(size + (size & 1)), cancellationToken);
            if (channels is null && Is(chunk, 0, "fmt "))
            {
                channels = Format(body.AsSpan(0, (int)size));
            }
        }
    }

    private static TakeWavHeader Data(int channels, uint size, long maxDataBytes)
    {
        if (size > maxDataBytes)
        {
            throw new TakeWavException(TakeWavError.TooLarge, string.Create(CultureInfo.InvariantCulture, $"The audio data is larger than {maxDataBytes} bytes."));
        }

        var header = new TakeWavHeader(channels, size);
        return size % header.BlockAlign == 0 ? header : throw Invalid("The audio data does not end on a whole frame.");
    }

    /// <summary>Validates the fmt chunk; returns the channel count.</summary>
    private static int Format(ReadOnlySpan<byte> fmt)
    {
        if (fmt.Length < 16)
        {
            throw Invalid("The fmt chunk is too short.");
        }

        var format = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]);
        var rate = BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..]);
        var blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(fmt[12..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]);
        CheckFormat(format, fmt);
        Require(channels is 1 or 2, "channels", $"The file has {channels} channels; record with 1 or 2.");
        Require(rate == RecordingConstants.SampleRate, "sampleRate", $"The sample rate is {rate} Hz; it must be 44,100 Hz.");
        Require(bits == RecordingConstants.BitDepth, "bitsPerSample", $"The file has {bits} bits per sample; it must be 24.");
        return blockAlign == 3 * channels ? channels : throw Invalid("The block size does not match 24-bit samples.");
    }

    private static void CheckFormat(ushort format, ReadOnlySpan<byte> fmt)
    {
        if (format == FormatPcm)
        {
            return;
        }

        Require(format == FormatExtensible, "audioFormat", $"The audio format code is {format}; only PCM is accepted.");
        if (fmt.Length < ExtensibleFmtSize)
        {
            throw Invalid("The extensible fmt chunk is too short.");
        }

        var subformat = fmt.Slice(24, 16);
        Require(BinaryPrimitives.ReadUInt16LittleEndian(subformat) == FormatPcm && subformat[2..].SequenceEqual(PcmSubformatTail), "audioFormat", "The extensible subformat is not PCM.");
    }

    private static void Require(bool valid, string field, string message)
    {
        if (!valid)
        {
            throw new TakeWavException(TakeWavError.Unsupported, message, field);
        }
    }

    private static bool Is(byte[] buffer, int offset, string id) =>
        buffer[offset] == id[0] && buffer[offset + 1] == id[1] && buffer[offset + 2] == id[2] && buffer[offset + 3] == id[3];

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        try
        {
            await stream.ReadExactlyAsync(buffer, cancellationToken);
        }
        catch (EndOfStreamException exception)
        {
            throw new TakeWavException("The file ends inside its header.", exception);
        }

        return buffer;
    }

    private static TakeWavException Invalid(string message) => new(TakeWavError.Invalid, message);
}
