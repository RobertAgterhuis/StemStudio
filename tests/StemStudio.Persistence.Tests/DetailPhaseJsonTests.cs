using System.Text.Json;
using StemStudio.Domain.Tracks.Detail;
using StemStudio.Persistence.Configurations;

namespace StemStudio.Persistence.Tests;

public sealed class DetailPhaseJsonTests
{
    private static readonly DetailPhase Queued = new()
    {
        Status = DetailPhaseStatus.Queued,
        Stage = "Queued",
        RequestedAt = TestData.T0,
        UpdatedAt = TestData.T0,
    };

    [Fact]
    public void Comparer_ComparesByContentAndHandlesNull()
    {
        var comparer = DetailPhaseJson.Comparer;
        var sameContent = Queued with { Parents = [] };

        comparer.Equals(null, null).ShouldBeTrue();
        comparer.Equals(Queued, null).ShouldBeFalse();
        comparer.Equals(Queued, sameContent).ShouldBeTrue();
        comparer.Equals(Queued, Queued with { Progress = 1 }).ShouldBeFalse();
        comparer.GetHashCode(null!).ShouldBe(string.Empty.GetHashCode(StringComparison.Ordinal));
        comparer.GetHashCode(Queued).ShouldBe(comparer.GetHashCode(sameContent));
        comparer.Snapshot(Queued).ShouldBeSameAs(Queued);
    }

    [Fact]
    public void Serialize_RoundTripsAndRejectsNullDocuments()
    {
        DetailPhaseJson.Deserialize(DetailPhaseJson.Serialize(Queued)).ShouldBe(Queued with { Parents = [] }, DetailPhaseJson.Comparer);
        Should.Throw<JsonException>(() => DetailPhaseJson.Deserialize("null"));
    }
}
