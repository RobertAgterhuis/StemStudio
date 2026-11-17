using System.Buffers.Binary;
using StemStudio.Domain.Recording;

namespace StemStudio.Domain.Tests.Recording;

/// <summary>Feature 015 T020/T022: streamed peak scan and the padded capture file (data-model 2.3, 2.4).</summary>
public sealed class PeakAndPaddingTests
{
    [Fact]
    public void AllZero_IsSilent_AtTheFloor()
    {
        var scanner = new PeakScanner();
        scanner.Add(TestWav.Samples(0, 0, 0));

        scanner.Result().ShouldBe(new PeakScan(-150, 0, true));
    }

    [Fact]
    public void FullScale_IsClipped_AtZeroDbfs()
    {
        var scanner = new PeakScanner();
        scanner.Add(TestWav.Samples(8_388_607, -8_388_608, 10, -8_388_607));

        var result = scanner.Result();

        (result.PeakDbfs, result.ClippedSamples, result.AllZero).ShouldBe((0, 3, false));
    }

    [Fact]
    public void Quiet_PeakIsComputedFromTheLargestMagnitude()
    {
        var scanner = new PeakScanner();
        scanner.Add(TestWav.Samples(100, -8_389, 50));

        scanner.Result().PeakDbfs.ShouldBe(-60, 0.01);
    }

    [Fact]
    public void SamplesSplitAcrossChunks_AreReassembled()
    {
        var bytes = TestWav.Samples(1, -4_194_304, 3);
        var scanner = new PeakScanner();
        foreach (var single in bytes)
        {
            scanner.Add([single]);
        }

        scanner.Result().PeakDbfs.ShouldBe(-6.02, 0.01);
    }

    [Fact]
    public void ToDbfs_FloorsVerySmallLevels()
    {
        PeakScanner.ToDbfs(0).ShouldBe(-150);
        PeakScanner.ToDbfs(1).ShouldBe(-138.47, 0.01);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1000)]
    public void Header_IsCanonical(int channels, long frames)
    {
        var header = TakeWavPadder.Header(channels, frames);

        header.Length.ShouldBe(44);
        System.Text.Encoding.ASCII.GetString(header, 0, 4).ShouldBe("RIFF");
        BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)).ShouldBe((uint)(36 + (frames * 3 * channels)));
        BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(22)).ShouldBe((ushort)channels);
        BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(28)).ShouldBe((uint)(44_100 * 3 * channels));
        BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(40)).ShouldBe((uint)(frames * 3 * channels));
    }

    [Fact]
    public void Header_RefusesSizesBeyondRiff()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => TakeWavPadder.Header(2, uint.MaxValue / 3));
        Should.Throw<ArgumentOutOfRangeException>(() => TakeWavPadder.Header(3, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => TakeWavPadder.Header(1, -1));
    }

    [Fact]
    public async Task Write_PadsWithZeroFrames_ThenCopiesExactlyTheData_AndScansIt()
    {
        var data = TestWav.Tone(30_000, 2, -200_000);
        var source = new MemoryStream(TestWav.Wav(data, 2).Concat(new byte[] { 9, 9, 9 }).ToArray());
        var header = await TakeWavReader.ReadHeaderAsync(source, long.MaxValue, TestContext.Current.CancellationToken);
        var output = new MemoryStream();

        var scan = await TakeWavPadder.WriteAsync(output, header, 25_000, source, TestContext.Current.CancellationToken);

        var written = output.ToArray();
        written.Length.ShouldBe(44 + (25_000 * 6) + data.Length);
        written.AsSpan(0, 44).ToArray().ShouldBe(TakeWavPadder.Header(2, 55_000));
        written.AsSpan(44, 25_000 * 6).ToArray().ShouldAllBe(value => value == 0);
        written.AsSpan(44 + (25_000 * 6)).ToArray().ShouldBe(data);
        scan.AllZero.ShouldBeFalse();
        scan.PeakDbfs.ShouldBe(20 * Math.Log10(200_000 / 8_388_608.0), 0.001);
    }

    [Fact]
    public async Task Write_TruncatedData_IsInvalid()
    {
        var file = TestWav.Riff(TestWav.Chunk("fmt ", TestWav.Fmt()), TestWav.DataChunk(TestWav.Tone(2), declaredSize: 300));
        var source = new MemoryStream(file);
        var header = await TakeWavReader.ReadHeaderAsync(source, long.MaxValue, TestContext.Current.CancellationToken);

        var refused = await Should.ThrowAsync<TakeWavException>(() => TakeWavPadder.WriteAsync(new MemoryStream(), header, 0, source, TestContext.Current.CancellationToken));

        refused.Error.ShouldBe(TakeWavError.Invalid);
    }
}
