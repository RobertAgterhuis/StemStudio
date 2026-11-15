using StemStudio.Domain.Generations;
using StemStudio.Domain.Tests.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Generations;

public sealed class TrackOriginTests
{
    [Fact]
    public void ImportedTracksHaveNoOrigin() => DetailTestData.CompletedTrack().Origin.ShouldBeNull();

    [Fact]
    public void MarkGenerated_SetsTheOriginOnce()
    {
        var track = DetailTestData.CompletedTrack();
        var origin = new TrackOrigin(Guid.NewGuid(), 2, GenerationKind.Restyle, GenerationTestData.TrackId);

        track.MarkGenerated(origin, DetailTestData.T0.AddMinutes(1)).ShouldBeTrue();
        track.MarkGenerated(origin with { TakeIndex = 1 }, DetailTestData.T0.AddMinutes(2)).ShouldBeFalse();

        track.Origin.ShouldBe(origin);
        track.Origin!.AiGenerated.ShouldBeTrue();
        track.UpdatedAt.ShouldBe(DetailTestData.T0.AddMinutes(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void TrackOrigin_TakeIndexIsOneOrTwo(int index) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new TrackOrigin(Guid.NewGuid(), index, GenerationKind.Restyle, null));
}
