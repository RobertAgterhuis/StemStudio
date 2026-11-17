using System.Buffers.Binary;

namespace StemStudio.Api.LibraryCare.Archive;

/// <summary>
/// Reads the entry count that a ZIP's end of central directory record declares (APPNOTE 4.3.14-4.3.16, Zip64 included)
/// without reading the central directory, so an archive that claims more entries than a backup may have is refused
/// before <see cref="System.IO.Compression.ZipArchive"/> allocates them (data-model 3.4 step 2, H17).
/// </summary>
public static class ZipDirectory
{
    private const uint EndSignature = 0x06054b50;
    private const uint Zip64LocatorSignature = 0x07064b50;
    private const uint Zip64EndSignature = 0x06064b50;
    private const int EndLength = 22;
    private const int LocatorLength = 20;
    private const int Zip64EndLength = 56;
    private const int MaxCommentLength = 0xFFFF;

    /// <summary>The declared number of entries, or null when no end record is found (left to the ZIP reader).</summary>
    /// <param name="stream">A readable, seekable archive stream; its position is restored.</param>
    /// <returns>The declared count.</returns>
    public static long? DeclaredEntries(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var start = stream.Position;
        try
        {
            var tailLength = (int)Math.Min(stream.Length, EndLength + MaxCommentLength);
            var tail = Read(stream, stream.Length - tailLength, tailLength);
            var end = LastSignature(tail, EndSignature, EndLength);
            if (end < 0)
            {
                return null;
            }

            var offset = stream.Length - tailLength + end;
            return Zip64Entries(stream, tail, end, offset) ?? BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end + 10));
        }
        finally
        {
            stream.Position = start;
        }
    }

    private static long? Zip64Entries(Stream stream, byte[] tail, int end, long endOffset)
    {
        var locator = end - LocatorLength;
        if (locator < 0 || BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(locator)) != Zip64LocatorSignature)
        {
            return null;
        }

        var recordOffset = BinaryPrimitives.ReadUInt64LittleEndian(tail.AsSpan(locator + 8));
        if (endOffset < Zip64EndLength || recordOffset > (ulong)(endOffset - Zip64EndLength))
        {
            return null;
        }

        var record = Read(stream, (long)recordOffset, Zip64EndLength);
        var total = BinaryPrimitives.ReadUInt64LittleEndian(record.AsSpan(32));
        return BinaryPrimitives.ReadUInt32LittleEndian(record) == Zip64EndSignature ? (long)Math.Min(total, long.MaxValue) : null;
    }

    private static int LastSignature(byte[] buffer, uint signature, int recordLength)
    {
        for (var index = buffer.Length - recordLength; index >= 0; index--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(index)) == signature)
            {
                return index;
            }
        }

        return -1;
    }

    private static byte[] Read(Stream stream, long offset, int length)
    {
        var buffer = new byte[length];
        stream.Position = offset;
        stream.ReadExactly(buffer);
        return buffer;
    }
}
