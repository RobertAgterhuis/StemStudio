using StemStudio.Web.Experience;
using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T084: the recording help topics of data-model 5 (FR-019, FR-020; one connection topic per instrument family).</summary>
public sealed class RecordingHelpTests
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    public static TheoryData<string> TopicIds() =>
    [
        "recording", "recording-permissions", "recording-connect-vocals", "recording-connect-guitar-bass", "recording-connect-keys",
        "recording-connect-drums", "recording-connect-acoustic", "recording-latency", "recording-monitoring", "recording-loop-takes",
        "recording-compare", "recording-export", "recording-speed", "recording-timing",
    ];

    [Theory]
    [MemberData(nameof(TopicIds))]
    public void EveryDataModel5Topic_Exists_InTheMixerArea_ReviewedFor015(string id)
    {
        var topic = Catalog.Topics.SingleOrDefault(t => t.Id == id);

        topic.ShouldNotBeNull(id);
        (topic.Area, topic.Reviewed).ShouldBe(("mixer", "015"));
        topic.Markdown.ShouldNotContain("Full text follows");
    }

    [Fact]
    public void TheOverview_StatesThePrivacyRule_AndLinksEveryFamily()
    {
        var overview = string.Join(" ", Catalog.Topics.Single(t => t.Id == "recording").Markdown.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        overview.ShouldContain("stay on this computer");
        foreach (var family in new[] { "vocals", "guitar-bass", "keys", "drums", "acoustic" })
        {
            overview.ShouldContain($"help:recording-connect-{family}");
        }
    }

    [Fact]
    public void Limits_AreDocumented()
    {
        var text = string.Join(' ', Catalog.Topics.Where(t => t.Id.StartsWith("recording", StringComparison.Ordinal)).SelectMany(t => t.Markdown.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));

        text.ShouldContain("100 %");
        text.ShouldContain("at most two channels");
        text.ShouldContain("200 takes");
        text.ShouldContain("Chrome or Edge");
        text.ShouldContain("clap test");
    }

    [Fact]
    public void Timing_IsAnExpertTopic()
    {
        var timing = Catalog.Topics.Single(t => t.Id == "recording-timing");

        timing.Levels.ShouldBe([ExperienceLevel.Expert]);
    }
}
