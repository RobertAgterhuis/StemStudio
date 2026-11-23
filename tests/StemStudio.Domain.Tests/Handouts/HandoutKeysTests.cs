using StemStudio.Domain.Handouts;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;
using static StemStudio.Domain.Tests.InstrumentViews.ChartData;

namespace StemStudio.Domain.Tests.Handouts;

/// <summary>Code review 020 finding 2: the handout key from the chord chart, else a concert-pitch score, in the written key.</summary>
public sealed class HandoutKeysTests
{
    [Theory]
    [InlineData("A", KeyMode.Minor, 9)]
    [InlineData("F#", KeyMode.Major, 6)]
    [InlineData("B-", KeyMode.Major, 10)]
    [InlineData("E--", KeyMode.Major, 2)]
    [InlineData("C-", KeyMode.Major, 11)]
    [InlineData("B#", KeyMode.Minor, 0)]
    public void Parse_ReadsMusic21Tonics(string tonic, KeyMode mode, int pitchClass) =>
        HandoutKeys.Parse(Key(tonic, mode)).ShouldBe(new MusicalKey(pitchClass, mode));

    [Theory]
    [InlineData("")]
    [InlineData("H")]
    [InlineData("Bb")]
    [InlineData("C#-")]
    [InlineData("C####")]
    [InlineData(" C")]
    public void Parse_RefusesWhatItCannotRead(string tonic)
    {
        HandoutKeys.Parse(Key(tonic, KeyMode.Major)).ShouldBeNull();
        HandoutKeys.Parse(null).ShouldBeNull();
        HandoutKeys.Parse(new KeyInfo(null!, KeyMode.Major, 1, KeySource.Estimated)).ShouldBeNull();
    }

    [Fact]
    public void TheChordKey_WinsOverTheScores()
    {
        var chart = Chart(("C:maj", 1000)) with { Key = new SongKey(0, KeyMode.Major, 0.9, null) };

        HandoutKeys.Of(chart, [Item(NotationKind.Score, null, "A")], Transposition.BFlat).ShouldBe(("D major", "chords"));
    }

    [Fact]
    public void TheFirstConcertPitchScore_IsWrittenForTheTransposition()
    {
        var items = new[] { Item(NotationKind.Transposed, Transposition.BFlat, "D"), Item(NotationKind.Drums, null, "G"), Item(NotationKind.Score, Transposition.EFlat, "G"), Item(NotationKind.Tab, null, "H"), Item(NotationKind.Score, Transposition.C, "A") };

        HandoutKeys.Of(null, items, Transposition.BFlat).ShouldBe(("B minor", "score"));
        HandoutKeys.Of(null, items, Transposition.C).ShouldBe(("A minor", "score"));
        HandoutKeys.Of(null, [Item(NotationKind.Tab, null, null)], Transposition.F).ShouldBe(((string?)null, (string?)null));
        Should.Throw<ArgumentNullException>(() => HandoutKeys.Of(null, null!, Transposition.C));
        Should.Throw<ArgumentNullException>(() => HandoutKeys.IsConcertPitch(null!));
    }

    private static KeyInfo Key(string tonic, KeyMode mode) => new(tonic, mode, 0.8, KeySource.Estimated);

    private static NotationItem Item(NotationKind kind, Transposition? transposition, string? tonic) => new()
    {
        AssetId = Guid.NewGuid(),
        Title = "Score",
        TrackId = Guid.NewGuid(),
        Profile = ScoreProfile.Bass,
        Kind = kind,
        Transposition = transposition,
        PdfPath = "assets/x/score.pdf",
        Key = tonic is null ? null : Key(tonic, KeyMode.Minor),
        CreatedAt = DateTime.UnixEpoch,
    };
}
