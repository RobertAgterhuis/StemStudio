using StemStudio.Web.Help;

namespace StemStudio.Web.Tests.Help;

/// <summary>Small sample help content for the catalogue, renderer and search tests.</summary>
public static class HelpSamples
{
    public static string Topic(
        string id,
        string area = "mixer",
        string? title = null,
        string summary = "A short summary.",
        string extra = "",
        string body = "Body text.") => $"""
        ---
        id: {id}
        title: {title ?? id}
        area: {area}
        summary: {summary}
        reviewed: "004"
        {extra}
        ---

        {body}
        """;

    public static HelpFile TopicFile(string id, string area = "mixer", string? content = null) =>
        new($"topics/{area}/{id}.md", content ?? Topic(id, area));

    public static HelpFile TourFile(string id, string json) => new($"tours/{id}.json", json);

    public static readonly HelpFile[] Files =
    [
        new("topics/mixer/mixer-pan.md", Topic("mixer-pan", title: "Pan", summary: "Moves a stem between the left and right speaker.",
            extra: "feature: mixer.pan\nlevels: [advanced, expert]\nanchors: [mixer-pan]\nkeywords: [stereo, balance]\nrelated: [mixer-screen]",
            body: "Pan places a stem in the stereo field. See [the mixer](help:mixer-screen).")),
        new("topics/mixer/mixer-screen.md", Topic("mixer-screen", title: "Mixer", summary: "Mix the stems of a song.",
            extra: "route: /tracks/{id}/mixer\nanchors: [strip-*, mixer-screen]",
            body: "## Channels\n\nEach strip is one stem.\n\n## Transport\n\nPlay and stop.")),
        new("topics/glossary/stem.md", Topic("stem", area: "glossary", title: "Stem", summary: "One separated instrument part of a song.",
            extra: "keywords: [part, track]", body: "A stem is one part.")),
        new("topics/start/welcome.md", Topic("welcome", area: "start", title: "Welcome to StemStudio", summary: "Pick a level.",
            extra: "anchors: [welcome, strip-vocals]")),
        new("tours/mixer-basics.json", """
            { "id": "mixer-basics", "version": 2, "title": "Mixer basics", "route": "/tracks/{id}/mixer", "levels": ["beginner"],
              "steps": [ { "target": "mixer-pan", "topic": "mixer-pan" }, { "target": "mixer-screen", "topic": "mixer-screen", "title": "Mixer", "text": "All stems." } ] }
            """),
    ];
}
