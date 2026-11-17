namespace StemStudio.Domain.Recording;

/// <summary>The level facts of a take's audio (data-model 2.1).</summary>
/// <param name="PeakDbfs">Peak in dBFS, floored at -150.</param>
/// <param name="ClippedSamples">Samples with <c>abs(v) &gt;= 2^23 - 1</c>.</param>
/// <param name="AllZero">True when every sample is digital silence.</param>
public sealed record PeakScan(double PeakDbfs, int ClippedSamples, bool AllZero);

/// <summary>
/// Streams over signed 24-bit little-endian samples once (in arbitrary chunks; a sample may be split between two chunks)
/// and computes the peak, the clipped count and "all zero".
/// </summary>
public sealed class PeakScanner
{
    /// <summary>Full scale of a 24-bit sample.</summary>
    public const double FullScale = 8_388_608;

    /// <summary>Smallest magnitude counted as clipped.</summary>
    public const int ClipThreshold = 8_388_607;

    private readonly byte[] _carry = new byte[3];
    private int _carried;
    private int _peak;
    private int _clipped;

    /// <summary>Adds the next bytes.</summary>
    /// <param name="bytes">Sample bytes.</param>
    public void Add(ReadOnlySpan<byte> bytes)
    {
        while (_carried > 0 && !bytes.IsEmpty)
        {
            _carry[_carried++] = bytes[0];
            bytes = bytes[1..];
            if (_carried == 3)
            {
                Sample(_carry);
                _carried = 0;
            }
        }

        if (bytes.IsEmpty)
        {
            return;
        }

        var whole = bytes.Length - (bytes.Length % 3);
        for (var i = 0; i < whole; i += 3)
        {
            Sample(bytes.Slice(i, 3));
        }

        bytes[whole..].CopyTo(_carry);
        _carried = bytes.Length - whole;
    }

    /// <summary>The result of the bytes added so far (a trailing partial sample is ignored).</summary>
    /// <returns>The scan.</returns>
    public PeakScan Result() => new(ToDbfs(_peak), _clipped, _peak == 0);

    /// <summary>A 24-bit magnitude in dBFS, floored at -150.</summary>
    /// <param name="magnitude">0..2^23.</param>
    /// <returns>The level.</returns>
    public static double ToDbfs(int magnitude) =>
        magnitude == 0 ? RecordingConstants.FloorDbfs : Math.Max(RecordingConstants.FloorDbfs, Math.Min(0, 20 * Math.Log10(magnitude / FullScale)));

    private void Sample(ReadOnlySpan<byte> sample)
    {
        var value = sample[0] | (sample[1] << 8) | ((sbyte)sample[2] << 16);
        var magnitude = Math.Abs(value);
        _peak = Math.Max(_peak, magnitude);
        _clipped += magnitude >= ClipThreshold ? 1 : 0;
    }
}
