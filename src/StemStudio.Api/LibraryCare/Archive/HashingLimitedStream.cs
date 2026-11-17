using System.Security.Cryptography;

namespace StemStudio.Api.LibraryCare.Archive;

/// <summary>
/// A write-only stream that forwards to an inner stream while hashing (SHA-256) and counting, and fails once more than
/// <see cref="Limit"/> bytes arrive (restore: extraction bounded by the declared size, research R-06 H9).
/// </summary>
public sealed class HashingLimitedStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    /// <summary>Wraps a stream.</summary>
    /// <param name="inner">The destination (not disposed by this stream).</param>
    /// <param name="limit">Most bytes accepted; <see cref="long.MaxValue"/> for no limit.</param>
    public HashingLimitedStream(Stream inner, long limit)
    {
        ArgumentNullException.ThrowIfNull(inner);
        (_inner, Limit) = (inner, limit);
    }

    /// <summary>Most bytes accepted.</summary>
    public long Limit { get; }

    /// <summary>Bytes written so far.</summary>
    public long Count { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => Count;

    /// <inheritdoc />
    public override long Position { get => Count; set => throw new NotSupportedException(); }

    /// <summary>The SHA-256 of everything written (lower-case hex); call once at the end.</summary>
    /// <returns>The hash.</returns>
    public string Sha256() => Convert.ToHexStringLower(_hash.GetHashAndReset());

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Accept(buffer);
        _inner.Write(buffer);
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Accept(buffer.Span);
        await _inner.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Accept(ReadOnlySpan<byte> buffer)
    {
        if (Count + buffer.Length > Limit)
        {
            throw new InvalidDataException("More bytes than declared.");
        }

        Count += buffer.Length;
        _hash.AppendData(buffer);
    }
}
