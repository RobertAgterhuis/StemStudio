using StemStudio.Domain.Insights;

namespace StemStudio.Domain.Tests.Insights;

/// <summary>Feature 020 T020: the stem files version of a quality report (data-model 5.1 <c>StemsVersion</c>).</summary>
public sealed class StemsVersionTests
{
    private static readonly DateTime T0 = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheVersion_IsHexOfFixedLength_AndIndependentOfOrder()
    {
        var a = new StemFileStamp("vocals", 100, T0);
        var b = new StemFileStamp("drums.kick", 200, T0.AddSeconds(1));

        var version = StemsVersion.Of([a, b]);

        version.Length.ShouldBe(StemsVersion.Length);
        version.ShouldMatch("^[0-9a-f]{32}$");
        StemsVersion.Of([b, a]).ShouldBe(version);
    }

    [Fact]
    public void AChangedSizeTimeOrChannel_ChangesTheVersion()
    {
        var baseline = StemsVersion.Of([new StemFileStamp("vocals", 100, T0)]);

        StemsVersion.Of([new StemFileStamp("vocals", 101, T0)]).ShouldNotBe(baseline);
        StemsVersion.Of([new StemFileStamp("vocals", 100, T0.AddTicks(1))]).ShouldNotBe(baseline);
        StemsVersion.Of([new StemFileStamp("drums", 100, T0)]).ShouldNotBe(baseline);
        StemsVersion.Of([new StemFileStamp("vocals", 100, T0), new StemFileStamp("bass", 1, T0)]).ShouldNotBe(baseline);
        Should.Throw<ArgumentNullException>(() => StemsVersion.Of(null!));
    }
}
