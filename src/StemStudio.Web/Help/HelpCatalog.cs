using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

namespace StemStudio.Web.Help;

/// <summary>
/// All help topics and tours (contracts/help-content.md), validated once when built: every file parses, ids
/// are unique and match their file names, related topics, <c>help:</c> links and tour topics resolve. Any
/// violation throws <see cref="HelpContentException"/> (start-up fails; the help tests catch it in CI).
/// </summary>
public sealed partial class HelpCatalog : IHelpCatalog
{
    private const string ResourcePrefix = "StemStudio.Web.Help.";

    private readonly Dictionary<string, HelpTopic> _byId = new(StringComparer.Ordinal);
    private readonly List<HelpTopic> _topics = [];
    private readonly List<TourDefinition> _tours = [];
    private readonly ConcurrentDictionary<string, RenderedTopic> _rendered = new(StringComparer.Ordinal);

    /// <summary>Builds and validates the catalogue.</summary>
    /// <param name="files">Topic and tour files.</param>
    public HelpCatalog(IEnumerable<HelpFile> files)
    {
        foreach (var file in files)
        {
            Add(file);
        }

        ValidateLinks();
        Areas = [.. TopicParser.AreaOrder.Where(a => _topics.Any(t => t.Area == a))];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<HelpTopic> Topics => _topics;

    /// <inheritdoc />
    public IReadOnlyList<string> Areas { get; }

    /// <inheritdoc />
    public IReadOnlyList<TourDefinition> Tours => _tours;

    [GeneratedRegex(@"\]\(help:([^)\s]*)\)")]
    private static partial Regex HelpLink();

    /// <summary>Builds the catalogue from the embedded resources of an assembly.</summary>
    /// <param name="assembly">Normally <c>StemStudio.Web</c>.</param>
    /// <returns>The catalogue.</returns>
    public static HelpCatalog FromAssembly(Assembly assembly) => new(EmbeddedFiles(assembly));

    /// <summary>Reads every embedded help file of an assembly.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The files, by path.</returns>
    public static IReadOnlyList<HelpFile> EmbeddedFiles(Assembly assembly) =>
        assembly.GetManifestResourceNames()
            .Order(StringComparer.Ordinal)
            .Select(name => (Name: name, Path: ResourcePath(name)))
            .Where(r => r.Path is not null)
            .Select(r => new HelpFile(r.Path!, Read(assembly, r.Name)))
            .ToList();

    /// <summary>Maps a manifest resource name to a help path.</summary>
    /// <param name="resourceName">For example <c>StemStudio.Web.Help.topics.mixer.mixer-pan.md</c>.</param>
    /// <returns><c>topics/mixer/mixer-pan.md</c>, or <see langword="null"/> for other resources.</returns>
    public static string? ResourcePath(string resourceName)
    {
        if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = resourceName[ResourcePrefix.Length..].Split('.');
        return parts.Length switch
        {
            4 when parts[0] == "topics" => $"topics/{parts[1]}/{parts[2]}.{parts[3]}",
            3 when parts[0] == "tours" => $"tours/{parts[1]}.{parts[2]}",
            _ => null,
        };
    }

    /// <inheritdoc />
    public HelpTopic? Topic(string id) => _byId.GetValueOrDefault(id);

    /// <inheritdoc />
    public HelpTopic? ScreenTopic(string route) => _topics.FirstOrDefault(t => t.Route == route);

    /// <inheritdoc />
    public HelpTopic? TopicForAnchor(string helpId) =>
        _topics.FirstOrDefault(t => t.Anchors.Contains(helpId, StringComparer.Ordinal))
        ?? _topics.FirstOrDefault(t => t.Anchors.Any(a => IsPatternMatch(a, helpId)));

    /// <inheritdoc />
    public RenderedTopic Render(string id) =>
        _rendered.GetOrAdd(id, key => HelpRenderer.Render(Topic(key) ?? throw new KeyNotFoundException($"Unknown help topic '{key}'.")));

    /// <summary>True when an anchor pattern such as <c>strip-*</c> covers a marker.</summary>
    /// <param name="anchor">A literal anchor or a pattern ending in <c>*</c>.</param>
    /// <param name="helpId">The marker.</param>
    /// <returns>Whether the pattern matches.</returns>
    internal static bool IsPatternMatch(string anchor, string helpId) =>
        anchor.EndsWith('*') && helpId.StartsWith(anchor[..^1], StringComparison.Ordinal);

    private static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void Add(HelpFile file)
    {
        var parts = file.Path.Split('/');
        switch (parts)
        {
            case ["topics", var area, var name] when name.EndsWith(".md", StringComparison.Ordinal):
                AddTopic(file, TopicParser.Parse(file, area, name[..^3]));
                break;
            case ["tours", var name] when name.EndsWith(".json", StringComparison.Ordinal):
                _tours.Add(TourParser.Parse(file, name[..^5]));
                break;
            default:
                throw HelpContentException.For(file.Path, "unexpected help file (expected topics/<area>/<id>.md or tours/<id>.json)");
        }
    }

    private void AddTopic(HelpFile file, HelpTopic topic)
    {
        if (!_byId.TryAdd(topic.Id, topic))
        {
            throw HelpContentException.For(file.Path, $"duplicate topic id '{topic.Id}'");
        }

        _topics.Add(topic);
    }

    private void ValidateLinks()
    {
        foreach (var topic in _topics)
        {
            var missing = topic.Related.FirstOrDefault(r => !_byId.ContainsKey(r));
            Fail(topic, missing, $"related topic '{missing}' does not exist");
            var link = HelpLink().Matches(topic.Markdown).Select(m => m.Groups[1].Value).FirstOrDefault(l => !_byId.ContainsKey(l));
            Fail(topic, link, $"link to unknown topic '{link}'");
        }

        foreach (var tour in _tours)
        {
            var index = tour.Steps.ToList().FindIndex(s => !_byId.ContainsKey(s.Topic));
            Fail($"tours/{tour.Id}.json", index >= 0, $"step {index + 1} uses unknown topic '{(index >= 0 ? tour.Steps[index].Topic : null)}'");
        }
    }

    private static void Fail(HelpTopic topic, string? culprit, string problem) =>
        Fail($"topics/{topic.Area}/{topic.Id}.md", culprit is not null, problem);

    private static void Fail(string path, bool failed, string problem)
    {
        if (failed)
        {
            throw HelpContentException.For(path, problem);
        }
    }
}
