using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Help;

/// <summary>Small sample help content for the catalogue, renderer and search tests.</summary>
public static class HelpSamples
{
    public static string Topic(string id, TopicText? text = null, string area = "mixer")
    {
        var t = text ?? new TopicText();
        return $"""
            ---
            id: {id}
            title: {t.Title ?? id}
            area: {area}
            summary: {t.Summary}
            reviewed: "004"
            {t.Extra}
            ---

            {t.Body}
            """;
    }

    public static HelpFile TopicFile(string id, string area = "mixer", string? content = null) =>
        new($"topics/{area}/{id}.md", content ?? Topic(id, area: area));

    public static HelpFile TourFile(string id, string json) => new($"tours/{id}.json", json);

    /// <summary>Front matter and body of a sample topic.</summary>
    public sealed record TopicText(string? Title = null, string Summary = "A short summary.", string Extra = "", string Body = "Body text.");

    public static readonly HelpFile[] Files =
    [
        new("topics/mixer/mixer-pan.md", Topic("mixer-pan", new("Pan", "Moves a stem between the left and right speaker.",
            "feature: mixer.pan\nlevels: [advanced, expert]\nanchors: [mixer-pan]\nkeywords: [stereo, balance]\nrelated: [mixer-screen]",
            "Pan places a stem in the stereo field. See [the mixer](help:mixer-screen)."))),
        new("topics/mixer/mixer-screen.md", Topic("mixer-screen", new("Mixer", "Mix the stems of a song.",
            "route: /tracks/{id}/mixer\nanchors: [strip-*, mixer-screen]",
            "## Channels\n\nEach strip is one stem.\n\n## Transport\n\nPlay and stop."))),
        new("topics/glossary/stem.md", Topic("stem", new("Stem", "One separated instrument part of a song.",
            "keywords: [part, track]", "A stem is one part."), "glossary")),
        new("topics/start/welcome.md", Topic("welcome", new("Welcome to StemStudio", "Pick a level.",
            "anchors: [welcome, strip-vocals]"), "start")),
        new("tours/mixer-basics.json", """
            { "id": "mixer-basics", "version": 2, "title": "Mixer basics", "route": "/tracks/{id}/mixer", "levels": ["beginner"],
              "steps": [ { "target": "mixer-pan", "topic": "mixer-pan" }, { "target": "mixer-screen", "topic": "mixer-screen", "title": "Mixer", "text": "All stems." } ] }
            """),
    ];
}
