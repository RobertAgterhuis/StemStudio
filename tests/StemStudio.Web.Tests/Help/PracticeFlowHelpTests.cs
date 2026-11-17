using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Help;

/// <summary>
/// Help content of feature 014 (data-model 6, FR-030; T061, T063, T069, T075, T085, T095, T101) on top of the 004 coverage suite
/// (markers, anchors, links, tour targets and schemas): every new topic id exists in its area, reviewed "014", with its anchors.
/// </summary>
public sealed class PracticeFlowHelpTests
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Theory]
    [InlineData("mixer-sections", "mixer", "section-lane")]
    [InlineData("section", "glossary", null)]
    [InlineData("speed-trainer-region", "glossary", "speed-trainer-region")]
    [InlineData("shortcuts", "start", "shortcut-overlay")]
    [InlineData("shortcuts-custom", "start", "shortcut-settings")]
    [InlineData("foot-pedals", "start", "pedal-setup")]
    [InlineData("mixer-sections-edit", "mixer", "section-editor")]
    [InlineData("mixer-sections-detection", "mixer", "section-status")]
    [InlineData("mixer-score-follow", "mixer", "score-panel")]
    public void Topics_Exist_ReviewedFor014_WithTheirAnchors(string id, string area, string? anchor)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull(id);

        topic.Area.ShouldBe(area);
        topic.Reviewed.ShouldBe("014");
        if (anchor is not null)
        {
            Catalog.TopicForAnchor(anchor)!.Id.ShouldBe(id);
        }
    }

    [Theory]
    [InlineData("practice-section", "beginner", "section-lane")]
    [InlineData("hands-free", "advanced", "section-loop-current")]
    public void Tours_Exist_ForTheirLevel(string id, string level, string firstTarget)
    {
        var tour = Catalog.Tours.Single(t => t.Id == id);

        tour.Version.ShouldBe(1);
        tour.Levels.Select(l => l.ToString().ToLowerInvariant()).ShouldContain(level);
        tour.Steps[0].Target.ShouldBe(firstTarget);
    }

    [Fact]
    public void ShortcutTopics_NameTheMovedKeys_TheSingleKeySwitch_AndTheReservedKeys()
    {
        Catalog.Topic("shortcuts")!.Markdown.ShouldContain("| 0 | Original speed and key |");
        Catalog.Topic("shortcuts")!.Markdown.ShouldContain("| Shift+K | Count-in on or off |");
        Catalog.Topic("shortcuts-custom")!.Markdown.ShouldContain("Single-key shortcuts");
        Catalog.Topic("shortcuts-custom")!.Route.ShouldBe("/settings/shortcuts");
        Catalog.Topic("foot-pedals")!.Markdown.ShouldContain("PageDown");
    }

    [Fact]
    public void SectionsTopic_NamesTheKeysAndThePartByPartRule()
    {
        var markdown = Catalog.Topic("mixer-sections")!.Markdown;

        markdown.ShouldContain("Shift+L");
        markdown.ShouldContain("Backspace");
        markdown.ShouldContain("part by part");
        markdown.ShouldContain("tempo grid is uncertain");
    }
}
