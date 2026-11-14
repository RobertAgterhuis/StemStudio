using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests;

public sealed class SkeletonTests
{
    [Fact]
    public void TrackStatus_HasTheSixContractStates()
    {
        Enum.GetNames<TrackStatus>().ShouldBe(["Queued", "Downloading", "Separating", "Analysing", "Completed", "Failed"]);
    }
}
