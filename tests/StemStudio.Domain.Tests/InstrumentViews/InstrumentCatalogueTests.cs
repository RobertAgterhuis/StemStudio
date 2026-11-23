using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Feature 016 T045: catalogue content = data-model 4.1-4.3 and the worker constants (<c>ports.TUNINGS</c>, <c>WRITTEN_INSTRUMENTS</c>).</summary>
public sealed class InstrumentCatalogueTests
{
    [Theory]
    [InlineData(TuningId.GuitarStandard, "guitar", "Standard (E A D G B E)", new[] { 40, 45, 50, 55, 59, 64 })]
    [InlineData(TuningId.GuitarDropD, "guitar", "Drop D", new[] { 38, 45, 50, 55, 59, 64 })]
    [InlineData(TuningId.GuitarHalfStepDown, "guitar", "Half step down (Eb)", new[] { 39, 44, 49, 54, 58, 63 })]
    [InlineData(TuningId.GuitarFullStepDown, "guitar", "Full step down (D)", new[] { 38, 43, 48, 53, 57, 62 })]
    [InlineData(TuningId.GuitarDadgad, "guitar", "DADGAD", new[] { 38, 45, 50, 55, 57, 62 })]
    [InlineData(TuningId.GuitarOpenG, "guitar", "Open G", new[] { 38, 43, 50, 55, 59, 62 })]
    [InlineData(TuningId.GuitarOpenD, "guitar", "Open D", new[] { 38, 45, 50, 54, 57, 62 })]
    [InlineData(TuningId.Bass4Standard, "bass", "4-string (E A D G)", new[] { 28, 33, 38, 43 })]
    [InlineData(TuningId.Bass4DropD, "bass", "4-string drop D", new[] { 26, 33, 38, 43 })]
    [InlineData(TuningId.Bass4HalfStepDown, "bass", "4-string half step down", new[] { 27, 32, 37, 42 })]
    [InlineData(TuningId.Bass5Standard, "bass", "5-string (B E A D G)", new[] { 23, 28, 33, 38, 43 })]
    [InlineData(TuningId.Bass6Standard, "bass", "6-string (B E A D G C)", new[] { 23, 28, 33, 38, 43, 48 })]
    public void Tunings_MatchDataModel41(TuningId id, string instrument, string label, int[] pitches)
    {
        var tuning = InstrumentCatalogue.TuningOf(id);

        (tuning.Id, tuning.Instrument, tuning.Label).ShouldBe((id, instrument, label));
        tuning.Pitches.ShouldBe(pitches);
        tuning.IsGuitar.ShouldBe(instrument == "guitar");
    }

    [Fact]
    public void Tunings_AreInEnumOrder() => InstrumentCatalogue.Tunings.Select(t => t.Id).ShouldBe(Enum.GetValues<TuningId>());

    public static TheoryData<WrittenInstrument, (string Name, string Key, string Interval, int Semitones), InstrumentRange> Instruments => new()
    {
        { WrittenInstrument.Concert, ("", "C", "P1", 0), new InstrumentRange(ClefChoice.Auto, 0, 127) },
        { WrittenInstrument.TrumpetBb, ("Trumpet in B-flat", "Bb", "M2", 2), new InstrumentRange(ClefChoice.Treble, 52, 82) },
        { WrittenInstrument.ClarinetBb, ("Clarinet in B-flat", "Bb", "M2", 2), new InstrumentRange(ClefChoice.Treble, 50, 91) },
        { WrittenInstrument.SopranoSaxBb, ("Soprano Saxophone in B-flat", "Bb", "M2", 2), new InstrumentRange(ClefChoice.Treble, 56, 87) },
        { WrittenInstrument.TenorSaxBb, ("Tenor Saxophone in B-flat", "Bb", "M9", 14), new InstrumentRange(ClefChoice.Treble, 44, 75) },
        { WrittenInstrument.BassClarinetBb, ("Bass Clarinet in B-flat", "Bb", "M9", 14), new InstrumentRange(ClefChoice.Treble, 38, 77) },
        { WrittenInstrument.AltoSaxEb, ("Alto Saxophone in E-flat", "Eb", "M6", 9), new InstrumentRange(ClefChoice.Treble, 49, 80) },
        { WrittenInstrument.BaritoneSaxEb, ("Baritone Saxophone in E-flat", "Eb", "M13", 21), new InstrumentRange(ClefChoice.Treble, 36, 68) },
        { WrittenInstrument.ClarinetEb, ("Clarinet in E-flat", "Eb", "-m3", -3), new InstrumentRange(ClefChoice.Treble, 53, 91) },
        { WrittenInstrument.HornF, ("Horn in F", "F", "P5", 7), new InstrumentRange(ClefChoice.Treble, 34, 77) },
        { WrittenInstrument.EnglishHornF, ("English Horn in F", "F", "P5", 7), new InstrumentRange(ClefChoice.Treble, 52, 81) },
        { WrittenInstrument.Flute, ("Flute", "C", "P1", 0), new InstrumentRange(ClefChoice.Treble, 60, 96) },
        { WrittenInstrument.Oboe, ("Oboe", "C", "P1", 0), new InstrumentRange(ClefChoice.Treble, 58, 91) },
        { WrittenInstrument.Violin, ("Violin", "C", "P1", 0), new InstrumentRange(ClefChoice.Treble, 55, 100) },
        { WrittenInstrument.Viola, ("Viola", "C", "P1", 0), new InstrumentRange(ClefChoice.Alto, 48, 88) },
        { WrittenInstrument.Cello, ("Cello", "C", "P1", 0), new InstrumentRange(ClefChoice.Bass, 36, 76) },
        { WrittenInstrument.DoubleBass, ("Double Bass", "C", "P8", 12), new InstrumentRange(ClefChoice.Bass, 28, 67) },
        { WrittenInstrument.Trombone, ("Trombone", "C", "P1", 0), new InstrumentRange(ClefChoice.Bass, 40, 72) },
        { WrittenInstrument.Bassoon, ("Bassoon", "C", "P1", 0), new InstrumentRange(ClefChoice.Bass, 34, 75) },
        { WrittenInstrument.Tuba, ("Tuba", "C", "P1", 0), new InstrumentRange(ClefChoice.Bass, 28, 65) },
    };

    [Theory]
    [MemberData(nameof(Instruments))]
    public void Instruments_MatchDataModel42(WrittenInstrument id, (string Name, string Key, string Interval, int Semitones) expected, InstrumentRange range)
    {
        var info = InstrumentCatalogue.InstrumentOf(id);

        (info.Id, info.Name, info.KeyName, info.Interval, info.Semitones).ShouldBe((id, expected.Name, expected.Key, expected.Interval, expected.Semitones));
        info.Range.ShouldBe(range);
    }

    [Fact]
    public void Instruments_AreInEnumOrder() => InstrumentCatalogue.Instruments.Select(i => i.Id).ShouldBe(Enum.GetValues<WrittenInstrument>());

    [Fact]
    public void Clefs_AreTheSevenChoicesOf43() =>
        InstrumentCatalogue.Clefs.ShouldBe([ClefChoice.Auto, ClefChoice.Treble, ClefChoice.Bass, ClefChoice.Alto, ClefChoice.Tenor, ClefChoice.Treble8vb, ClefChoice.Bass8vb]);

    [Fact]
    public void UnknownInterval_Throws() => Should.Throw<ArgumentException>(() => InstrumentCatalogue.IntervalSemitones("A4"));
}
