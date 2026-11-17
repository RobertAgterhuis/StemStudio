using Microsoft.Extensions.Time.Testing;
using StemStudio.Api.LibraryCare.Archive;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 CTO/QA request: <c>StemStudio:LibraryCare:ThrottleBytesPerSecond</c> slows archive streams so E2E can see a running job.</summary>
public sealed class ThrottledStreamTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Default_IsOff() => new LibraryCareOptions().ThrottleBytesPerSecond.ShouldBe(0);

    [Fact]
    public void Off_ReturnsTheInnerStream()
    {
        using var inner = new MemoryStream();

        ByteThrottle.Wrap(inner, 0, TimeProvider.System).ShouldBeSameAs(inner);
    }

    [Fact]
    public async Task On_WritesAtMostTheRate()
    {
        var clock = new FakeTimeProvider();
        using var inner = new MemoryStream();
        await using var throttled = ByteThrottle.Wrap(inner, 50, clock);

        var write = throttled.WriteAsync(new byte[100], Ct).AsTask();
        await Task.Delay(50, Ct);
        write.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(2));
        await write.WaitAsync(TimeSpan.FromSeconds(5), Ct);

        inner.Length.ShouldBe(100);
    }

    [Fact]
    public async Task On_SyncWritesAndFlushPassThrough()
    {
        using var inner = new MemoryStream();
        await using var throttled = ByteThrottle.Wrap(inner, long.MaxValue, TimeProvider.System);

        throttled.Write([1, 2, 3], 0, 3);
        await throttled.FlushAsync(Ct);
        throttled.Flush();

        (inner.Length, throttled.CanWrite, throttled.CanRead, throttled.CanSeek, throttled.Length, throttled.Position).ShouldBe((3, true, false, false, 3, 3));
        Should.Throw<NotSupportedException>(() => throttled.Read(new byte[1], 0, 1));
        Should.Throw<NotSupportedException>(() => throttled.Seek(0, SeekOrigin.Begin));
        Should.Throw<NotSupportedException>(() => throttled.SetLength(1));
        Should.Throw<NotSupportedException>(() => throttled.Position = 1);
    }
}
