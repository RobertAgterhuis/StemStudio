using StemStudio.Contracts.Tracks;
using StemStudio.Web.Services;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Services;

/// <summary>The track library order is a pure function of the tracks: sort-key ties break on the id, not on the order the API returned (r22 flake fix).</summary>
public sealed class TrackListQueryTests
{
    private static readonly Guid LowId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid MidId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid HighId = Guid.Parse("33333333-0000-0000-0000-000000000003");

    private static TrackDto Tied(Guid id) => TestData.Track() with { Id = id, Title = "Same title", Artist = "Same artist", CreatedAt = TestData.Created };

    public static TheoryData<TrackSort> Sorts => new(Enum.GetValues<TrackSort>());

    [Theory]
    [MemberData(nameof(Sorts))]
    public void Ties_AreOrderedByIdWhateverTheInputOrder(TrackSort sort)
    {
        var tracks = new[] { Tied(MidId), Tied(HighId), Tied(LowId) };

        foreach (var input in Permutations(tracks))
        {
            TrackListQuery.Apply(input, search: null, sort).Select(t => t.Id).ShouldBe([LowId, MidId, HighId]);
        }
    }

    [Fact]
    public void Newest_StillSortsByCreatedAtBeforeTheTieBreak()
    {
        var newer = Tied(HighId) with { CreatedAt = TestData.Created.AddSeconds(1) };

        TrackListQuery.Apply([Tied(LowId), newer, Tied(MidId)], search: null, TrackSort.Newest).Select(t => t.Id).ShouldBe([HighId, LowId, MidId]);
    }

    private static IEnumerable<TrackDto[]> Permutations(TrackDto[] items) =>
        items.SelectMany(a => items.Where(b => b != a).SelectMany(b => items.Where(c => c != a && c != b).Select(c => new[] { a, b, c })));
}
