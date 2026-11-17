using System.Buffers.Binary;
using System.IO.Compression;
using StemStudio.Api.LibraryCare.Archive;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>Security review 013 item 4: the declared entry count is read from the end records before the directory.</summary>
public sealed class ZipDirectoryTests
{
    [Fact]
    public void PlainArchive_DeclaresItsEntryCount()
    {
        using var stream = Zip(3);

        ZipDirectory.DeclaredEntries(stream).ShouldBe(3);
        stream.Position.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(100)]
    public void NoEndRecord_IsLeftToTheZipReader(int length)
    {
        using var stream = new MemoryStream(new byte[length]);

        ZipDirectory.DeclaredEntries(stream).ShouldBeNull();
    }

    [Fact]
    public void Zip64Locator_WithAnOffsetOutsideTheFile_FallsBackToTheEndRecord()
    {
        var bytes = Zip(2).ToArray();
        var withLocator = new byte[bytes.Length + 20];
        var end = bytes.Length - 22;
        bytes.AsSpan(0, end).CopyTo(withLocator);
        BinaryPrimitives.WriteUInt32LittleEndian(withLocator.AsSpan(end), 0x07064b50);
        BinaryPrimitives.WriteUInt64LittleEndian(withLocator.AsSpan(end + 8), ulong.MaxValue);
        bytes.AsSpan(end).CopyTo(withLocator.AsSpan(end + 20));

        ZipDirectory.DeclaredEntries(new MemoryStream(withLocator)).ShouldBe(2);
    }

    [Fact]
    public void Zip64Locator_PointingAtAnotherRecord_FallsBackToTheEndRecord()
    {
        var bytes = Zip(1).ToArray();
        var withLocator = new byte[bytes.Length + 20];
        var end = bytes.Length - 22;
        bytes.AsSpan(0, end).CopyTo(withLocator);
        BinaryPrimitives.WriteUInt32LittleEndian(withLocator.AsSpan(end), 0x07064b50);
        BinaryPrimitives.WriteUInt64LittleEndian(withLocator.AsSpan(end + 8), 0);
        bytes.AsSpan(end).CopyTo(withLocator.AsSpan(end + 20));

        ZipDirectory.DeclaredEntries(new MemoryStream(withLocator)).ShouldBe(1);
    }

    private static MemoryStream Zip(int entries)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var index = 0; index < entries; index++)
            {
                zip.CreateEntry($"e{index}.txt");
            }
        }

        stream.Position = 0;
        return stream;
    }
}
