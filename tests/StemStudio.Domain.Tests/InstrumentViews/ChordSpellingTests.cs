using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Feature 016 T041: <see cref="ChordSpeller"/> against every <c>cases</c> and <c>capo</c> vector (data-model 2.5, 2.9).</summary>
public sealed class ChordSpellingTests
{
    public static TheoryData<int> Cases() => SpellingVectors.Indexes("cases");

    public static TheoryData<int> CapoCases() => SpellingVectors.Indexes("capo");

    [Fact]
    public void Vectors_AreVersion100WithAtLeast200Cases()
    {
        SpellingVectors.Document.GetProperty("version").GetString().ShouldBe("1.0.0");
        Cases().Count.ShouldBeGreaterThanOrEqualTo(200);
        SpellingVectors.Document.GetProperty("rules").GetArrayLength().ShouldBe(10);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryCase_SpellsTheExpectedSymbol(int index)
    {
        var vector = SpellingVectors.At("cases", index);
        var chord = new ChordName(SpellingVectors.Int(vector, "root"), SpellingVectors.Quality(vector), SpellingVectors.Int(vector, "bass"));
        MusicalKey? key = SpellingVectors.Int(vector, "keyTonic") is { } tonic ? new MusicalKey(tonic, SpellingVectors.Mode(SpellingVectors.Str(vector, "keyMode")!)) : null;
        var display = new ChordDisplay(vector.GetProperty("transpose").GetInt32(), vector.GetProperty("capo").GetInt32());

        ChordSpeller.Symbol(chord, key, display).ShouldBe(vector.GetProperty("symbol").GetString());
    }

    [Theory]
    [MemberData(nameof(CapoCases))]
    public void EveryCapoCase_ShowsTheExpectedShape(int index)
    {
        var vector = SpellingVectors.At("capo", index);
        var chord = new ChordName(SpellingVectors.Int(vector, "root"), SpellingVectors.Quality(vector), SpellingVectors.Int(vector, "bass"));
        var display = new ChordDisplay(vector.GetProperty("transpose").GetInt32(), vector.GetProperty("capo").GetInt32());

        ChordSpeller.Symbol(chord, new MusicalKey(0, KeyMode.Major), display).ShouldBe(vector.GetProperty("shape").GetString());
    }

    [Theory]
    [InlineData(6, KeyMode.Major, true)]
    [InlineData(3, KeyMode.Minor, true)]
    [InlineData(2, KeyMode.Major, false)]
    [InlineData(9, KeyMode.Minor, false)]
    [InlineData(2, KeyMode.Minor, true)]
    public void UsesFlats_FollowsTheRelativeMajor(int tonic, KeyMode mode, bool flats) =>
        ChordSpeller.UsesFlats(new MusicalKey(tonic, mode)).ShouldBe(flats);

    [Fact]
    public void NoteName_WrapsNegativeValues() => ChordSpeller.NoteName(-1, flats: true).ShouldBe("B");

    [Fact]
    public void Move_WithoutKey_IsNull() => ChordSpeller.Move(null, new ChordDisplay(3, 0)).ShouldBeNull();

    [Fact]
    public void Display_ShiftIsTransposeMinusCapo()
    {
        new ChordDisplay(5, 2).Shift.ShouldBe(3);
        ChordDisplay.Sounding.Shift.ShouldBe(0);
    }

    [Fact]
    public void Symbol_RejectsNull() => Should.Throw<ArgumentNullException>(() => ChordSpeller.Symbol(null!, null, ChordDisplay.Sounding));

    [Fact]
    public void Qualities_HaveHarteNamesAndSuffixes()
    {
        foreach (var quality in Enum.GetValues<ChordQuality>())
        {
            ChordQualities.FromHarte(ChordQualities.HarteName(quality)).ShouldBe(quality);
        }

        ChordQualities.Suffix(ChordQuality.HalfDim7).ShouldBe("m7b5");
        ChordQualities.FromHarte("power").ShouldBeNull();
    }
}
