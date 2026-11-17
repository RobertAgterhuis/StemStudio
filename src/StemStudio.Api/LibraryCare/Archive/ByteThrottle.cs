namespace StemStudio.Api.LibraryCare.Archive;

/// <summary>
/// Optional rate limit of archive streams (<c>StemStudio:LibraryCare:ThrottleBytesPerSecond</c>, default 0 = off). Only the
/// E2E compose override sets it, so a running backup, verify or restore stays observable (cancel, write gate).
/// </summary>
public static class ByteThrottle
{
    /// <summary>Wraps a destination stream when a rate is set.</summary>
    /// <param name="inner">The destination.</param>
    /// <param name="bytesPerSecond">The rate; 0 or less = no throttle.</param>
    /// <param name="clock">Clock of the delays.</param>
    /// <returns>The inner stream, or a throttled wrapper that does not dispose it.</returns>
    public static Stream Wrap(Stream inner, long bytesPerSecond, TimeProvider clock) =>
        bytesPerSecond <= 0 ? inner : new ThrottledStream(inner, bytesPerSecond, clock);

    private sealed class ThrottledStream(Stream inner, long bytesPerSecond, TimeProvider clock) : Stream
    {
        private readonly long _started = clock.GetTimestamp();
        private long _written;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _written;

        public override long Position { get => _written; set => throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken);
            _written += buffer.Length;
            var due = TimeSpan.FromSeconds((double)_written / bytesPerSecond) - clock.GetElapsedTime(_started);
            if (due > TimeSpan.Zero)
            {
                await Task.Delay(due, clock, cancellationToken);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
