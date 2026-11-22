using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Feature 016 T041: Harte parse of the chord edit pattern (data-model 2.1, FR-015).</summary>
public sealed class HarteLabelTests
{
    [Theory]
    [InlineData("C:maj", 0, ChordQuality.Maj, null)]
    [InlineData("A:min7/b7", 9, ChordQuality.Min7, 7)]
    [InlineData("C:maj/3", 0, ChordQuality.Maj, 4)]
    [InlineData("C:maj/5", 0, ChordQuality.Maj, 7)]
    [InlineData("G:7/7", 7, ChordQuality.Seventh, 6)]
    [InlineData("F#:hdim7", 6, ChordQuality.HalfDim7, null)]
    [InlineData("Bb:min6", 10, ChordQuality.Min6, null)]
    [InlineData("Cb:maj", 11, ChordQuality.Maj, null)]
    [InlineData("B#:minmaj7", 0, ChordQuality.MinMaj7, null)]
    [InlineData("E:sus4/1", 4, ChordQuality.Sus4, null)]
    [InlineData("D:dim7/b3", 2, ChordQuality.Dim7, 5)]
    public void Chords_ParseToPitchClasses(string label, int root, ChordQuality quality, int? bass)
    {
        HarteLabel.TryParse(label, out var chord).ShouldBeTrue();

        chord.ShouldBe(new ChordName(root, quality, bass));
    }

    [Theory]
    [InlineData("N")]
    [InlineData("X")]
    public void NoChordAndUnknown_HaveNoRoot(string label)
    {
        HarteLabel.TryParse(label, out var chord).ShouldBeTrue();

        chord.ShouldBe(ChordName.None);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C")]
    [InlineData("c:maj")]
    [InlineData("H:maj")]
    [InlineData("C:power")]
    [InlineData("C:maj/8")]
    [InlineData("C:maj/#3")]
    [InlineData("C:maj ")]
    [InlineData("C:maj\n")]
    [InlineData("N:maj")]
    [InlineData("C:maj/3/5")]
    [InlineData("C:maj;{title: x}")]
    public void InvalidLabels_AreRejected(string? label) => HarteLabel.TryParse(label, out _).ShouldBeFalse();

    [Fact]
    public void OverlongLabel_IsRejectedBeforeMatching() => HarteLabel.TryParse(new string('C', 33), out _).ShouldBeFalse();
}
