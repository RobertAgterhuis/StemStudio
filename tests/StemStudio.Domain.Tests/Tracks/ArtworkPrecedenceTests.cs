using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Tracks;

/// <summary>Feature 005 T016: artwork precedence (data-model 3.1, FR-005).</summary>
public sealed class ArtworkPrecedenceTests
{
    private static readonly DateTime T0 = DetailTestData.T0;
    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);

    [Theory]
    [InlineData(ArtworkSource.User, 0)]
    [InlineData(ArtworkSource.Spotify, 1)]
    [InlineData(ArtworkSource.Embedded, 2)]
    [InlineData(ArtworkSource.CoverArtArchive, 3)]
    [InlineData(ArtworkSource.Youtube, 4)]
    public void Rank_FollowsTheFixedOrder(ArtworkSource source, int rank)
    {
        ArtworkPrecedence.Rank(source).ShouldBe(rank);
    }

    [Fact]
    public void Rank_RejectsUnknownSources()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ArtworkPrecedence.Rank((ArtworkSource)42));
    }

    [Fact]
    public void Replaces_AnyCandidateWhenNothingIsShown()
    {
        ArtworkPrecedence.Replaces(null, new ArtworkCandidate(ArtworkSource.Youtube, HashA)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ArtworkSource.Youtube, ArtworkSource.CoverArtArchive, true)]
    [InlineData(ArtworkSource.CoverArtArchive, ArtworkSource.Spotify, true)]
    [InlineData(ArtworkSource.Spotify, ArtworkSource.CoverArtArchive, false)]
    [InlineData(ArtworkSource.Embedded, ArtworkSource.Youtube, false)]
    public void Replaces_OnlyABetterRank(ArtworkSource current, ArtworkSource offered, bool replaces)
    {
        var shown = new TrackArtwork(current, HashA, T0);

        ArtworkPrecedence.Replaces(shown, new ArtworkCandidate(offered, HashB)).ShouldBe(replaces);
    }

    [Fact]
    public void Replaces_EqualRankOnlyWithANewHash()
    {
        var shown = new TrackArtwork(ArtworkSource.Youtube, HashA, T0);

        ArtworkPrecedence.Replaces(shown, new ArtworkCandidate(ArtworkSource.Youtube, HashA)).ShouldBeFalse();
        ArtworkPrecedence.Replaces(shown, new ArtworkCandidate(ArtworkSource.Youtube, HashB)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEF")]
    [InlineData("zz")]
    public void TrackArtwork_RejectsAnInvalidHash(string sha)
    {
        Should.Throw<DomainValidationException>(() => new TrackArtwork(ArtworkSource.Spotify, sha, T0));
        Should.Throw<DomainValidationException>(() => new ArtworkCandidate(ArtworkSource.Spotify, sha));
    }

    [Fact]
    public void TrackArtwork_RejectsAnUnknownSource()
    {
        Should.Throw<DomainValidationException>(() => new TrackArtwork((ArtworkSource)9, HashA, T0));
    }

    [Fact]
    public void TrackArtwork_VersionIsTheFirstTwelveHexCharacters()
    {
        var hash = "2c26b46b68ffc68ff99b453c1d30413413422d706483bfa0f98a5e886266e7ae";

        new TrackArtwork(ArtworkSource.CoverArtArchive, hash, T0).Version.ShouldBe("2c26b46b68ff");
    }

    [Fact]
    public void OfferArtwork_ShowsTheFirstAutomaticPicture()
    {
        var track = DetailTestData.CompletedTrack();

        track.OfferArtwork(new ArtworkCandidate(ArtworkSource.Youtube, HashA), T0.AddMinutes(1)).ShouldBeTrue();

        track.Artwork.ShouldBe(new TrackArtwork(ArtworkSource.Youtube, HashA, T0.AddMinutes(1)));
        track.AutoArtwork.ShouldBe(track.Artwork);
        track.UpdatedAt.ShouldBe(T0.AddMinutes(1));
    }

    [Fact]
    public void OfferArtwork_IgnoresAWorseCandidateWithoutChangingTheTrack()
    {
        var track = DetailTestData.CompletedTrack();
        track.OfferArtwork(new ArtworkCandidate(ArtworkSource.Spotify, HashA), T0);

        track.OfferArtwork(new ArtworkCandidate(ArtworkSource.CoverArtArchive, HashB), T0.AddMinutes(1)).ShouldBeFalse();

        track.Artwork!.Source.ShouldBe(ArtworkSource.Spotify);
        track.UpdatedAt.ShouldBe(T0);
    }

    [Fact]
    public void OfferArtwork_AcceptsAnEqualRankWithANewHash()
    {
        var track = DetailTestData.CompletedTrack();
        track.OfferArtwork(new ArtworkCandidate(ArtworkSource.CoverArtArchive, HashA), T0);

        track.OfferArtwork(new ArtworkCandidate(ArtworkSource.CoverArtArchive, HashB), T0.AddMinutes(1)).ShouldBeTrue();

        track.Artwork!.Sha256.ShouldBe(HashB);
    }

    [Fact]
    public void OfferArtwork_RejectsTheUserSource()
    {
        var track = DetailTestData.CompletedTrack();

        Should.Throw<ArgumentException>(() => track.OfferArtwork(new ArtworkCandidate(ArtworkSource.User, HashA), T0));
        track.AcceptsArtwork(new ArtworkCandidate(ArtworkSource.User, HashA)).ShouldBeFalse();
    }

    [Fact]
    public void AcceptsArtwork_TellsWhetherAnOfferWouldBeTaken()
    {
        var track = DetailTestData.CompletedTrack();
        track.OfferArtwork(new ArtworkCandidate(ArtworkSource.Embedded, HashA), T0);

        track.AcceptsArtwork(new ArtworkCandidate(ArtworkSource.Spotify, HashB)).ShouldBeTrue();
        track.AcceptsArtwork(new ArtworkCandidate(ArtworkSource.Youtube, HashB)).ShouldBeFalse();
    }

    [Fact]
    public void UserPicture_IsKeptOverAutomaticOffers()
    {
        var track = DetailTestData.CompletedTrack();
        track.SetUserArtwork(HashA, T0);

        track.OfferArtwork(new ArtworkCandidate(ArtworkSource.Spotify, HashB), T0.AddMinutes(1)).ShouldBeTrue();

        track.Artwork.ShouldBe(new TrackArtwork(ArtworkSource.User, HashA, T0));
        track.AutoArtwork.ShouldBe(new TrackArtwork(ArtworkSource.Spotify, HashB, T0.AddMinutes(1)));
        track.HasUserArtwork.ShouldBeTrue();
    }

    [Fact]
    public void ResetArtwork_ReturnsToTheAutomaticPicture()
    {
        var track = DetailTestData.CompletedTrack();
        track.OfferArtwork(new ArtworkCandidate(ArtworkSource.Youtube, HashB), T0);
        track.SetUserArtwork(HashA, T0.AddMinutes(1));

        track.ResetArtwork(T0.AddMinutes(2)).ShouldBeTrue();

        track.Artwork.ShouldBe(track.AutoArtwork);
        track.Artwork!.Source.ShouldBe(ArtworkSource.Youtube);
        track.HasUserArtwork.ShouldBeFalse();
        track.UpdatedAt.ShouldBe(T0.AddMinutes(2));
    }

    [Fact]
    public void ResetArtwork_WithoutAutomaticPictureShowsNone()
    {
        var track = DetailTestData.CompletedTrack();
        track.SetUserArtwork(HashA, T0);

        track.ResetArtwork(T0.AddMinutes(1)).ShouldBeTrue();

        track.Artwork.ShouldBeNull();
    }

    [Fact]
    public void ResetArtwork_IsANoOpWithoutUserPicture()
    {
        var track = DetailTestData.CompletedTrack();

        track.ResetArtwork(T0.AddMinutes(1)).ShouldBeFalse();

        track.UpdatedAt.ShouldBe(T0);
    }

    [Fact]
    public void SetUserArtwork_ReplacesAnEarlierUserPicture()
    {
        var track = DetailTestData.CompletedTrack();
        track.SetUserArtwork(HashA, T0);

        track.SetUserArtwork(HashB, T0.AddMinutes(1));

        track.Artwork.ShouldBe(new TrackArtwork(ArtworkSource.User, HashB, T0.AddMinutes(1)));
    }
}
