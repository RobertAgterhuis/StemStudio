using System.Buffers.Binary;

namespace StemStudio.Domain.Tests.Recording;

/// <summary>Builds take WAV files (and hostile variants) for the recording tests.</summary>
public static class TestWav
{
    public static readonly byte[] PcmSubformat =
        [0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71];

    /// <summary>24-bit samples (interleaved) as little-endian bytes.</summary>
    public static byte[] Samples(params int[] values)
    {
        var bytes = new byte[values.Length * 3];
        for (var i = 0; i < values.Length; i++)
        {
            bytes[i * 3] = (byte)values[i];
            bytes[(i * 3) + 1] = (byte)(values[i] >> 8);
            bytes[(i * 3) + 2] = (byte)(values[i] >> 16);
        }

        return bytes;
    }

    /// <summary>A constant tone of the given frames.</summary>
    public static byte[] Tone(long frames, int channels = 1, int value = 100_000) =>
        Samples(Enumerable.Repeat(value, (int)(frames * channels)).ToArray());

    /// <summary>A 16-byte fmt chunk body of the take format (PCM, mono, 44.1 kHz, 24-bit).</summary>
    public static byte[] Fmt() => Fmt(new WavFormat());

    /// <summary>A 16-byte fmt chunk body of the given format.</summary>
    public static byte[] Fmt(WavFormat format)
    {
        var fmt = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(fmt, format.Format);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(2), format.Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(4), format.Rate);
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(8), format.Rate * (uint)(3 * format.Channels));
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(12), format.BlockAlign ?? (ushort)(3 * format.Channels));
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(14), format.Bits);
        return fmt;
    }

    public static byte[] ExtensibleFmt(ushort channels = 1, byte[]? subformat = null)
    {
        var fmt = new byte[40];
        Fmt(new WavFormat { Format = 0xFFFE, Channels = channels }).CopyTo(fmt, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(16), 22);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(18), 24);
        (subformat ?? PcmSubformat).CopyTo(fmt, 24);
        return fmt;
    }

    public static byte[] Chunk(string id, byte[] body, uint? declaredSize = null)
    {
        var size = declaredSize ?? (uint)body.Length;
        var pad = body.Length % 2;
        var chunk = new byte[8 + body.Length + pad];
        System.Text.Encoding.ASCII.GetBytes(id).CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), size);
        body.CopyTo(chunk, 8);
        return chunk;
    }

    public static byte[] Riff(params byte[][] chunks)
    {
        var body = chunks.SelectMany(chunk => chunk).ToArray();
        var file = new byte[12 + body.Length];
        "RIFF"u8.CopyTo(file);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), (uint)(4 + body.Length));
        "WAVE"u8.CopyTo(file.AsSpan(8));
        body.CopyTo(file, 12);
        return file;
    }

    /// <summary>A valid take WAV.</summary>
    public static byte[] Wav(byte[] data, int channels = 1) => Riff(Chunk("fmt ", Fmt(new WavFormat { Channels = (ushort)channels })), DataChunk(data));

    /// <summary>A data chunk without the RIFF pad byte (data is always the last chunk).</summary>
    public static byte[] DataChunk(byte[] data, uint? declaredSize = null)
    {
        var chunk = new byte[8 + data.Length];
        "data"u8.CopyTo(chunk);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), declaredSize ?? (uint)data.Length);
        data.CopyTo(chunk, 8);
        return chunk;
    }
}

/// <summary>The fields of a WAV fmt chunk; the defaults are the take format.</summary>
public sealed record WavFormat
{
    /// <summary>The audio format tag (1 = PCM, 0xFFFE = extensible).</summary>
    public ushort Format { get; init; } = 1;

    /// <summary>The channel count.</summary>
    public ushort Channels { get; init; } = 1;

    /// <summary>The sample rate in Hz.</summary>
    public uint Rate { get; init; } = 44_100;

    /// <summary>The bits per sample.</summary>
    public ushort Bits { get; init; } = 24;

    /// <summary>The block align; <see langword="null"/> derives it from the channels.</summary>
    public ushort? BlockAlign { get; init; }
}
