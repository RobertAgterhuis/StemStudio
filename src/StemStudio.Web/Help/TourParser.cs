using System.Text.Json;
using System.Text.Json.Serialization;

namespace StemStudio.Web.Help;

/// <summary>Parses and validates one tour file (contracts/help-content.md 3).</summary>
internal static class TourParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Parses <c>tours/&lt;fileId&gt;.json</c>.</summary>
    /// <param name="file">The file.</param>
    /// <param name="fileId">The file name without extension.</param>
    /// <returns>The tour.</returns>
    public static TourDefinition Parse(HelpFile file, string fileId)
    {
        var dto = Deserialize(file);
        var problem = Problem(dto, fileId);
        if (problem is not null)
        {
            throw HelpContentException.For(file.Path, problem);
        }

        var levels = dto.Levels!.Select(l => TopicParser.Level(file.Path, l)).ToList();
        var steps = dto.Steps!.Select(s => new TourStep(s.Target ?? string.Empty, s.Topic ?? string.Empty, s.Title, s.Text)).ToList();
        return new TourDefinition(dto.Id!, dto.Version, dto.Title!, dto.Route!, levels, steps);
    }

    private static TourDto Deserialize(HelpFile file)
    {
        try
        {
            return JsonSerializer.Deserialize<TourDto>(file.Content, Options) ?? new TourDto();
        }
        catch (JsonException ex)
        {
            throw HelpContentException.For(file.Path, $"invalid JSON ({ex.Message})");
        }
    }

    private static string? Problem(TourDto tour, string fileId) => tour switch
    {
        { Id: null } or { Title: null } or { Route: null } => "missing id, title or route",
        _ when !TopicParser.KebabId().IsMatch(tour.Id) => $"invalid tour id '{tour.Id}'",
        _ when tour.Id != fileId => $"id '{tour.Id}' does not match the file name '{fileId}'",
        { Version: < 1 } => "version must be at least 1",
        _ when tour.Title.Length > 40 => "title is longer than 40 characters",
        _ when tour.Route != "*" && !tour.Route.StartsWith('/') => "route must be '*' or start with '/'",
        { Levels: null or { Count: 0 } } => "levels must not be empty",
        { Steps: null or { Count: < 1 or > 8 } } => "steps must have 1 to 8 entries",
        _ => StepProblem(tour.Steps),
    };

    private static string? StepProblem(List<StepDto> steps) =>
        steps.Select((step, index) => step switch
        {
            { Target: null or "" } or { Topic: null or "" } => $"step {index + 1} needs a target and a topic",
            { Text.Length: > 200 } => $"step {index + 1} text is longer than 200 characters",
            _ => null,
        }).FirstOrDefault(p => p is not null);

    private sealed class TourDto
    {
        public string? Id { get; set; }

        public int Version { get; set; }

        public string? Title { get; set; }

        public string? Route { get; set; }

        public List<string>? Levels { get; set; }

        public List<StepDto>? Steps { get; set; }
    }

    private sealed class StepDto
    {
        public string? Target { get; set; }

        public string? Topic { get; set; }

        public string? Title { get; set; }

        public string? Text { get; set; }
    }
}
