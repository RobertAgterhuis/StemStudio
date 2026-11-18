using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Feature 016 T041: <see cref="KeyFormatter"/> against every <c>keys</c> vector (data-model 2.8).</summary>
public sealed class KeyFormatterTests
{
    public static TheoryData<int> Keys() => SpellingVectors.Indexes("keys");

    [Theory]
    [MemberData(nameof(Keys))]
    public void EveryKey_HasTheExpectedDisplayAndWrittenKey(int index)
    {
        var vector = SpellingVectors.At("keys", index);
        var key = new MusicalKey(vector.GetProperty("tonic").GetInt32(), SpellingVectors.Mode(vector.GetProperty("mode").GetString()!));

        KeyFormatter.Display(key, vector.GetProperty("transpose").GetInt32(), vector.GetProperty("confidence").GetDouble())
            .ShouldBe(vector.GetProperty("display").GetString());
        KeyFormatter.WrittenKey(key, SpellingVectors.Instrument(vector.GetProperty("instrument").GetString()!))
            .ShouldBe(SpellingVectors.Str(vector, "writtenKey"));
    }

    [Fact]
    public void Name_SpellsEFlatMajorWithFlats() => KeyFormatter.Name(new MusicalKey(3, KeyMode.Major)).ShouldBe("Eb major");

    [Fact]
    public void Display_AtTheThreshold_IsCertain() => KeyFormatter.Display(new MusicalKey(0, KeyMode.Major), 0, 0.5).ShouldBe("C major");
}
