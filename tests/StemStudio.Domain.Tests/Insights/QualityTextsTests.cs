using StemStudio.Domain.Insights;

namespace StemStudio.Domain.Tests.Insights;

/// <summary>Feature 020 T044: the fixed reason texts (data-model 4.6) and the checks of the worker's result.</summary>
public sealed class QualityTextsTests
{
    [Theory]
    [InlineData(QualityReason.Clean, QualityLevel.Low, "drums", "Little bleed and few artefacts.")]
    [InlineData(QualityReason.Bleed, QualityLevel.Medium, "drums", "Some sound of drums is in this stem.")]
    [InlineData(QualityReason.Bleed, QualityLevel.High, "guitar", "Much of guitar is in this stem.")]
    [InlineData(QualityReason.Bleed, QualityLevel.Medium, null, "Some sound of another instrument is in this stem.")]
    [InlineData(QualityReason.BleedTogether, QualityLevel.Medium, "bass", "May be bleed of bass, or the two instruments playing together.")]
    [InlineData(QualityReason.Artefacts, QualityLevel.Low, null, "The stem has separation artefacts (short dropouts in the sound).")]
    [InlineData(QualityReason.BleedAndArtefacts, QualityLevel.High, "piano", "Bleed of piano and separation artefacts.")]
    [InlineData(QualityReason.NotPresent, QualityLevel.High, "guitar", "This instrument is probably not in this song; what you hear is spill from other instruments.")]
    [InlineData(QualityReason.TooShort, QualityLevel.Unknown, null, "Too little audio to rate.")]
    public void TheReasonText_FollowsTheCodeLevelAndSource(QualityReason reason, QualityLevel level, string? source, string text) =>
        QualityReasonTexts.For(Stem("vocals", reason, level, source)).ShouldBe(text);

    [Fact]
    public void TheReasonText_RefusesNull() => Should.Throw<ArgumentNullException>(() => QualityReasonTexts.For(null!));

    [Fact]
    public void AValidResult_Passes()
    {
        var stems = new[] { Stem("vocals", QualityReason.Bleed, QualityLevel.Medium, "drums"), Stem("drums.kick", QualityReason.Clean, QualityLevel.Low, null), Stem("drums.rest", QualityReason.Clean, QualityLevel.Low, null) };

        Should.NotThrow(() => QualityResultRules.Validate("1.0.0", stems));
        Should.NotThrow(() => QualityResultRules.Validate("fake-1", stems));
    }

    public static TheoryData<string, string?, StemQuality[]> Hostile => new()
    {
        { "no version", null, [Stem("vocals")] },
        { "long version", new string('1', 21), [Stem("vocals")] },
        { "script version", "<script>", [Stem("vocals")] },
        { "no stems", "1.0.0", [] },
        { "duplicate channel", "1.0.0", [Stem("vocals"), Stem("vocals")] },
        { "unknown channel", "1.0.0", [Stem("../etc")] },
        { "unknown detail", "1.0.0", [Stem("bass.kick")] },
        { "unknown main source", "1.0.0", [Stem("vocals") with { Bleed = new BleedEstimate(QualityLevel.Low, 0.1, "drums.kick", false, Reliability.Indicative) }] },
        { "index above one", "1.0.0", [Stem("vocals") with { Bleed = new BleedEstimate(QualityLevel.Low, 1.5, null, false, Reliability.Indicative) }] },
        { "negative artefact index", "1.0.0", [Stem("vocals") with { Artefacts = new ArtefactEstimate(QualityLevel.Low, -0.1, [], Reliability.Indicative) }] },
        { "NaN share", "1.0.0", [Stem("vocals") with { ActiveShare = double.NaN }] },
        { "infinite level", "1.0.0", [Stem("vocals") with { LevelDb = double.NegativeInfinity }] },
        { "negative seconds", "1.0.0", [Stem("vocals") with { ActiveSeconds = -1 }] },
        { "duplicate kinds", "1.0.0", [Stem("vocals") with { Artefacts = new ArtefactEstimate(QualityLevel.Low, 0.1, [ArtefactKind.Flicker, ArtefactKind.Flicker], Reliability.Indicative) }] },
        { "unknown rating", "1.0.0", [Stem("vocals") with { Rating = (QualityRating)42 }] },
        { "missing bleed", "1.0.0", [Stem("vocals") with { Bleed = null! }] },
        { "missing kinds", "1.0.0", [Stem("vocals") with { Artefacts = new ArtefactEstimate(QualityLevel.Low, 0.1, null!, Reliability.Indicative) }] },
        { "too many stems", "1.0.0", [.. Enumerable.Range(0, 65).Select(index => Stem(index % 2 == 0 ? "vocals" : "drums"))] },
    };

    [Theory]
    [MemberData(nameof(Hostile))]
    public void AnInvalidResult_IsRefused(string why, string? version, StemQuality[] stems) =>
        Should.Throw<DomainValidationException>(() => QualityResultRules.Validate(version, stems), why);

    [Fact]
    public void Validate_RefusesNull() => Should.Throw<ArgumentNullException>(() => QualityResultRules.Validate("1.0.0", null!));

    private static StemQuality Stem(string channel, QualityReason reason = QualityReason.Clean, QualityLevel level = QualityLevel.Low, string? source = null) => new(
        channel,
        QualityRating.Good,
        reason,
        100,
        0.5,
        -6,
        new BleedEstimate(level, 0.1, source, false, Reliability.Indicative),
        new ArtefactEstimate(QualityLevel.Low, 0.1, [], Reliability.Indicative));
}
