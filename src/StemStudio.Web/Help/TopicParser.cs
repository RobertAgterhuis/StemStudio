using System.Text.RegularExpressions;
using StemStudio.Web.Experience;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StemStudio.Web.Help;

/// <summary>Parses and validates one topic file: YAML front matter plus Markdown body (contracts/help-content.md 2).</summary>
internal static partial class TopicParser
{
    /// <summary>Allowed areas in contract order.</summary>
    public static readonly string[] AreaOrder = ["start", "import", "tracks", "detail", "mixer", "mixes", "instruments", "insights", "handouts", "glossary"];

    private const string Fence = "---";

    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    public static partial Regex KebabId();

    [GeneratedRegex("^[0-9]{3}$")]
    private static partial Regex Review();

    /// <summary>Parses a topic file at <c>topics/&lt;folder&gt;/&lt;fileId&gt;.md</c>.</summary>
    /// <param name="file">The file.</param>
    /// <param name="folder">The area folder.</param>
    /// <param name="fileId">The file name without extension.</param>
    /// <returns>The topic.</returns>
    public static HelpTopic Parse(HelpFile file, string folder, string fileId)
    {
        var (yaml, body) = Split(file);
        var topic = Topic(file, Deserialize(file, yaml), body);
        Validate(file, topic, folder, fileId);
        return topic;
    }

    private static HelpTopic Topic(HelpFile file, FrontMatter front, string body) => new()
    {
        Id = Required(file, front.Id, "id"),
        Title = Required(file, front.Title, "title"),
        Area = Required(file, front.Area, "area"),
        Summary = Required(file, front.Summary, "summary"),
        Feature = front.Feature,
        Levels = Levels(file, front.Levels),
        Route = front.Route,
        Anchors = front.Anchors ?? [],
        Keywords = front.Keywords ?? [],
        Related = front.Related ?? [],
        Reviewed = Required(file, front.Reviewed, "reviewed"),
        Markdown = body,
    };

    /// <summary>Parses a level name (lower-case).</summary>
    /// <param name="path">File path for the error.</param>
    /// <param name="name">The level name.</param>
    /// <returns>The level.</returns>
    public static ExperienceLevel Level(string path, string name) =>
        Enum.TryParse<ExperienceLevel>(name, ignoreCase: true, out var level) && string.Equals(name, name.ToLowerInvariant(), StringComparison.Ordinal) && Enum.IsDefined(level)
            ? level
            : throw HelpContentException.For(path, $"unknown level '{name}'");

    private static (string Yaml, string Body) Split(HelpFile file)
    {
        var text = file.Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var end = text.StartsWith(Fence + "\n", StringComparison.Ordinal) ? text.IndexOf("\n" + Fence, Fence.Length, StringComparison.Ordinal) : -1;
        if (end < 0)
        {
            throw HelpContentException.For(file.Path, "missing YAML front matter between '---' lines");
        }

        var bodyStart = text.IndexOf('\n', end + 1);
        return (text[(Fence.Length + 1)..end], bodyStart < 0 ? string.Empty : text[(bodyStart + 1)..]);
    }

    private static FrontMatter Deserialize(HelpFile file, string yaml)
    {
        try
        {
            return Yaml.Deserialize<FrontMatter?>(yaml) ?? new FrontMatter();
        }
        catch (YamlException ex)
        {
            throw HelpContentException.For(file.Path, $"invalid front matter ({ex.Message})");
        }
    }

    private static string Required(HelpFile file, string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? throw HelpContentException.For(file.Path, $"missing required field '{field}'") : value;

    private static List<ExperienceLevel> Levels(HelpFile file, List<string>? names) =>
        names?.Select(n => Level(file.Path, n)).ToList() ?? [];

    private static void Validate(HelpFile file, HelpTopic topic, string folder, string fileId)
    {
        var problem = Problem(topic, folder, fileId);
        if (problem is not null)
        {
            throw HelpContentException.For(file.Path, problem);
        }
    }

    private static string? Problem(HelpTopic topic, string folder, string fileId) => topic switch
    {
        _ when !KebabId().IsMatch(topic.Id) || topic.Id.Length > 48 => $"invalid topic id '{topic.Id}'",
        _ when topic.Id != fileId => $"id '{topic.Id}' does not match the file name '{fileId}'",
        _ when !AreaOrder.Contains(topic.Area) => $"unknown area '{topic.Area}'",
        _ when topic.Area != folder => $"area '{topic.Area}' does not match the folder '{folder}'",
        _ when topic.Title.Length > 60 => "title is longer than 60 characters",
        _ when topic.Summary.Length > 140 => "summary is longer than 140 characters",
        _ when !Review().IsMatch(topic.Reviewed) => $"reviewed '{topic.Reviewed}' is not a three-digit feature number",
        _ => null,
    };

    /// <summary>YAML shape of the front matter; unknown keys fail deserialization (additionalProperties false).</summary>
    internal sealed class FrontMatter
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Area { get; set; }

        public string? Summary { get; set; }

        public string? Feature { get; set; }

        public List<string>? Levels { get; set; }

        public string? Route { get; set; }

        public List<string>? Anchors { get; set; }

        public List<string>? Keywords { get; set; }

        public List<string>? Related { get; set; }

        public string? Reviewed { get; set; }
    }
}
