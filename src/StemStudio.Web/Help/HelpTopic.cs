using StemStudio.Web.Experience;

namespace StemStudio.Web.Help;

/// <summary>
/// One help topic: the front matter of <c>Help/topics/&lt;area&gt;/&lt;id&gt;.md</c> plus its Markdown body
/// (contracts/help-content.md 2, data-model.md 4).
/// </summary>
public sealed record HelpTopic
{
    /// <summary>Kebab-case id, equal to the file name, at most 48 characters.</summary>
    public required string Id { get; init; }

    /// <summary>Sentence-case title, 1-60 characters.</summary>
    public required string Title { get; init; }

    /// <summary>Area: <c>start</c>, <c>import</c>, <c>tracks</c>, <c>detail</c>, <c>mixer</c>, <c>mixes</c> or <c>glossary</c>.</summary>
    public required string Area { get; init; }

    /// <summary>Plain-text summary, 1-140 characters (tooltip and default tour text).</summary>
    public required string Summary { get; init; }

    /// <summary>Feature-catalogue id driving the level note, or <see langword="null"/>.</summary>
    public string? Feature { get; init; }

    /// <summary>Levels the topic is written for; empty means all levels.</summary>
    public IReadOnlyList<ExperienceLevel> Levels { get; init; } = [];

    /// <summary>Route template of a screen topic (exactly one per routable page), otherwise <see langword="null"/>.</summary>
    public string? Route { get; init; }

    /// <summary><c>data-help-id</c> values this topic explains; a trailing <c>-*</c> is a prefix pattern.</summary>
    public IReadOnlyList<string> Anchors { get; init; } = [];

    /// <summary>Extra search terms.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Ids of related topics (each must exist).</summary>
    public IReadOnlyList<string> Related { get; init; } = [];

    /// <summary>Three-digit number of the feature that last reviewed the text, e.g. <c>004</c>.</summary>
    public required string Reviewed { get; init; }

    /// <summary>Markdown body after the front matter.</summary>
    public required string Markdown { get; init; }
}
