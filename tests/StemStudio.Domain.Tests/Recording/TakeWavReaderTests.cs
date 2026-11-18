using StemStudio.Domain.Recording;

namespace StemStudio.Domain.Tests.Recording;

/// <summary>Feature 015 T020: upload validation of take WAVs (data-model 2.3), including hostile headers.</summary>
public sealed class TakeWavReaderTests
{
    private const long Max = 1024 * 1024;

    private static Task<TakeWavHeader> Read(byte[] file, long max = Max) =>
        TakeWavReader.ReadHeaderAsync(new MemoryStream(file), max, TestContext.Current.CancellationToken);

    private static async Task<TakeWavException> Refused(byte[] file, long max = Max) =>
        await Should.ThrowAsync<TakeWavException>(() => Read(file, max));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ValidPcm_ReturnsChannelsAndFrames_AndLeavesTheStreamAtTheData(int channels)
    {
        var data = TestWav.Tone(10, channels);
        var stream = new MemoryStream(TestWav.Wav(data, channels));

        var header = await TakeWavReader.ReadHeaderAsync(stream, Max, TestContext.Current.CancellationToken);

        (header.Channels, header.DataBytes, header.Frames, header.BlockAlign).ShouldBe((channels, data.Length, 10L, 3 * channels));
        stream.Position.ShouldBe(stream.Length - data.Length);
    }

    [Fact]
    public async Task Extensible_WithPcmSubformat_IsAccepted()
    {
        var header = await Read(TestWav.Riff(TestWav.Chunk("fmt ", TestWav.ExtensibleFmt(2)), TestWav.DataChunk(TestWav.Tone(4, 2))));

        header.Frames.ShouldBe(4);
    }

    [Fact]
    public async Task Extensible_WithFloatSubformat_IsUnsupported()
    {
        var floatFormat = (byte[])TestWav.PcmSubformat.Clone();
        floatFormat[0] = 3;

        var refused = await Refused(TestWav.Riff(TestWav.Chunk("fmt ", TestWav.ExtensibleFmt(1, floatFormat)), TestWav.DataChunk(TestWav.Tone(4))));

        (refused.Error, refused.Field).ShouldBe((TakeWavError.Unsupported, "audioFormat"));
    }

    [Fact]
    public async Task Extensible_ShortFmt_IsInvalid()
    {
        var refused = await Refused(TestWav.Riff(TestWav.Chunk("fmt ", TestWav.Fmt(new WavFormat { Format = 0xFFFE })), TestWav.DataChunk(TestWav.Tone(4))));

        refused.Error.ShouldBe(TakeWavError.Invalid);
    }

    [Fact]
    public async Task ExtraChunks_BeforeTheData_AreSkipped_IncludingOddSizedPadding()
    {
        var file = TestWav.Riff(TestWav.Chunk("LIST", new byte[7]), TestWav.Chunk("fmt ", TestWav.Fmt()), TestWav.Chunk("bext", new byte[33]), TestWav.DataChunk(TestWav.Tone(5)));

        (await Read(file)).Frames.ShouldBe(5);
    }

    [Fact]
    public async Task ASecondFmtChunk_IsIgnored()
    {
        var file = TestWav.Riff(TestWav.Chunk("fmt ", TestWav.Fmt()), TestWav.Chunk("fmt ", TestWav.Fmt(new WavFormat { Rate = 48_000 })), TestWav.DataChunk(TestWav.Tone(5)));

        (await Read(file)).Channels.ShouldBe(1);
    }

    [Fact]
    public async Task ChunksLargerThan64KiB_BeforeTheData_AreInvalid_WithoutReadingThem()
    {
        var hostile = TestWav.Riff(TestWav.Chunk("fmt ", TestWav.Fmt()), TestWav.Chunk("junk", [], declaredSize: uint.MaxValue));

        (await Refused(hostile)).Error.ShouldBe(TakeWavError.Invalid);
    }

    /// <summary>Formats other than the take format (all with block align 3), with the refused field.</summary>
    public static TheoryData<string, WavFormat> OtherFormats() => new()
    {
        { "audioFormat", new WavFormat { Format = 3, BlockAlign = 3 } },
        { "channels", new WavFormat { Channels = 3, BlockAlign = 3 } },
        { "channels", new WavFormat { Channels = 0, BlockAlign = 3 } },
        { "sampleRate", new WavFormat { Rate = 48_000, BlockAlign = 3 } },
        { "bitsPerSample", new WavFormat { Bits = 16, BlockAlign = 3 } },
    };

    [Theory]
    [MemberData(nameof(OtherFormats))]
    public async Task OtherFormats_AreUnsupported_WithTheField(string field, WavFormat format)
    {
        var fmt = TestWav.Fmt(format);

        var refused = await Refused(TestWav.Riff(TestWav.Chunk("fmt ", fmt), TestWav.DataChunk(TestWav.Tone(4))));

        (refused.Error, refused.Field).ShouldBe((TakeWavError.Unsupported, field));
        refused.Message.ShouldNotBeNullOrWhiteSpace();
    }

    public static TheoryData<string, byte[]> InvalidFiles() => new()
    {
        { "empty", [] },
        { "not riff", "RIFX\0\0\0\0WAVE"u8.ToArray() },
        { "not wave", "RIFF\0\0\0\0AVI "u8.ToArray() },
        { "truncated header", TestWav.Wav(TestWav.Tone(4))[..20] },
        { "data before fmt", TestWav.Riff(TestWav.DataChunk(TestWav.Tone(4)), TestWav.Chunk("fmt ", TestWav.Fmt())) },
        { "no data chunk", TestWav.Riff(TestWav.Chunk("fmt ", TestWav.Fmt())) },
        { "short fmt", TestWav.Riff(TestWav.Chunk("fmt ", new byte[14]), TestWav.DataChunk(TestWav.Tone(4))) },
        { "block align mismatch", TestWav.Riff(TestWav.Chunk("fmt ", TestWav.Fmt(new WavFormat { BlockAlign = 4 })), TestWav.DataChunk(TestWav.Tone(4))) },
        { "odd data size", TestWav.Riff(TestWav.Chunk("fmt ", TestWav.Fmt()), TestWav.DataChunk(new byte[7])) },
    };

    [Theory]
    [MemberData(nameof(InvalidFiles))]
    public async Task UnreadableFiles_AreInvalid(string name, byte[] file)
    {
        var refused = await Refused(file);

        refused.Error.ShouldBe(TakeWavError.Invalid, name);
        refused.Field.ShouldBeNull();
    }

    [Fact]
    public async Task DeclaredDataLargerThanTheLimit_IsTooLarge()
    {
        var hostile = TestWav.Riff(TestWav.Chunk("fmt ", TestWav.Fmt()), TestWav.DataChunk(TestWav.Tone(4), declaredSize: uint.MaxValue - 2));

        (await Refused(hostile, max: 1000)).Error.ShouldBe(TakeWavError.TooLarge);
    }

    [Fact]
    public async Task DataExactlyAtTheLimit_IsAccepted()
    {
        (await Read(TestWav.Wav(TestWav.Tone(4)), max: 12)).DataBytes.ShouldBe(12);
    }

    [Fact]
    public void Exception_Constructors_DefaultToInvalid()
    {
        new TakeWavException().Error.ShouldBe(TakeWavError.Invalid);
        new TakeWavException("x").Message.ShouldBe("x");
        new TakeWavException("x", new IOException()).Error.ShouldBe(TakeWavError.Invalid);
    }
}
