using StemStudio.Domain.Styles;

namespace StemStudio.Domain.Tests.Styles;

public sealed class StylePromptBuilderTests
{
    [Fact]
    public void Caption_PresetVector() =>
        StylePromptBuilder.Caption(StyleTestData.Preset().Snapshot())
            .ShouldBe("Jazz swing, swing feel, piano, upright bass, brushed drums, relaxed, warm");

    [Fact]
    public void Caption_UserVectorAddsInstrumentalAndDescription() =>
        StylePromptBuilder.Caption(StyleTestData.User().Snapshot())
            .ShouldBe("Jazz cool jazz, laidback feel, muted trumpet, piano, upright bass, melancholic, intimate, instrumental, Late-night trio with a soft muted trumpet.");

    [Fact]
    public void Caption_KeepTempoAndEmptyPartsAreLeftOut()
    {
        var fields = StyleTestData.RainyCafe() with
        {
            GenreFamily = GenreFamily.HipHop,
            SubGenre = null,
            TempoFeel = TempoFeel.Keep,
            Instrumentation = [],
            Moods = [],
            VocalHandling = VocalHandling.Keep,
            Description = null,
        };

        StylePromptBuilder.Caption(StyleTestData.User(fields).Snapshot()).ShouldBe("Hip hop");
    }

    [Theory]
    [InlineData(TempoFeel.Driving, "Rock, driving feel")]
    [InlineData(TempoFeel.HalfTime, "Rock, half-time feel")]
    public void Caption_TempoPhrases(TempoFeel feel, string expected)
    {
        var fields = StyleTestData.RainyCafe() with
        {
            GenreFamily = GenreFamily.Rock,
            SubGenre = null,
            TempoFeel = feel,
            Instrumentation = [],
            Moods = [],
            VocalHandling = VocalHandling.Keep,
            Description = null,
        };

        StylePromptBuilder.Caption(StyleTestData.User(fields).Snapshot()).ShouldBe(expected);
    }

    [Fact]
    public void Caption_IsTrimmedAtAWordBoundary()
    {
        var words = string.Join(' ', Enumerable.Repeat("abcdefghij", 60));
        var snapshot = StyleTestData.User().Snapshot() with { Fields = StyleTestData.RainyCafe() with { Description = words } };

        var caption = StylePromptBuilder.Caption(snapshot);

        caption.Length.ShouldBeLessThanOrEqualTo(StylePromptBuilder.MaxCaptionLength);
        caption.ShouldEndWith("abcdefghij");
    }

    [Fact]
    public void TrimToWords_CutsAWordWithoutSpaces()
    {
        StylePromptBuilder.TrimToWords(new string('a', 600), 512).Length.ShouldBe(512);
        StylePromptBuilder.TrimToWords("short", 512).ShouldBe("short");
    }

    [Theory]
    [InlineData(Strength.Subtle, 0.85)]
    [InlineData(Strength.Light, 0.70)]
    [InlineData(Strength.Balanced, 0.55)]
    [InlineData(Strength.Strong, 0.40)]
    [InlineData(Strength.Free, 0.25)]
    public void CoverStrength_FollowsTheScale(Strength strength, double expected) =>
        StylePromptBuilder.CoverStrength(strength).ShouldBe(expected);

    [Fact]
    public void Build_UsesTheTurboDefaults()
    {
        var prompt = StylePromptBuilder.Build(StyleTestData.Preset().Snapshot(), Strength.Strong);

        prompt.Caption.ShouldStartWith("Jazz swing");
        prompt.AudioCoverStrength.ShouldBe(0.40);
        prompt.InferenceSteps.ShouldBe(8);
        prompt.Shift.ShouldBe(3.0);
    }

    [Fact]
    public void FamilyLabels_CoverEveryFamily()
    {
        foreach (var family in Enum.GetValues<GenreFamily>())
        {
            StylePromptBuilder.FamilyLabel(family).ShouldNotBeNullOrWhiteSpace();
        }
    }
}
