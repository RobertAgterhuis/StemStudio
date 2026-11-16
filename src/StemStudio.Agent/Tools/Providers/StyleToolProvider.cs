using System.Text.Json.Nodes;
using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using static StemStudio.Agent.Tools.Providers.GenerationTools;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>
/// The style transfer and style library tools of feature 009 (tool catalogue 1.0.0). The model passes only <c>styleId</c>s to
/// <c>restyle_track</c>; style free text is checked by the artist-imitation guardrail (FR-025).
/// </summary>
public sealed class StyleToolProvider : IAgentToolProvider
{
    private const string F = "009";
    private const int MaxStyles = 30;
    private const int MaxGenerations = 20;
    private static readonly ToolParameter GenerationId = Param.Uuid("generationId").Describe("Generation id from list_generations.");
    private static readonly ToolParameter Tag = Param.Text("tag", 1, ContractLimits.StyleTagMaxLength);
    private static readonly string[] Genres = ["rock", "pop", "jazz", "blues", "electronic", "hipHop", "latin", "folk", "classical", "world", "country", "metal", "soul", "ambient"];
    private static readonly string[] Strengths = ["subtle", "light", "balanced", "strong", "free"];

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        Create("get_generation_status", ToolRiskDto.Read) with
        {
            Title = "Get the generator status",
            Description = "Whether the optional music generator is installed and ready (GPU), and the voice service state.",
            Availability = ToolAvailability.Always,
            Handler = StatusAsync,
        },
        Create("list_styles", ToolRiskDto.Read) with
        {
            Title = "List styles",
            Description = "List preset and own styles (genre, tempo feel, vocals, strength). Use the styleId with restyle_track or generate_song. Search a style or genre word with query (e.g. 'reggae'); genreFamily accepts only the listed families.",
            Schema = ToolSchema.Of(Param.Text("query", 1, 60).Optional(), Param.Choice("kind", "preset", "user").Optional(), Param.Choice("genreFamily", Genres).Optional()),
            Handler = ListStylesAsync,
        },
        Create("create_style", ToolRiskDto.Change) with
        {
            Title = "Create a style",
            Description = "Save an own style: genre family, tempo feel, instruments and moods (up to 8 each). Styles describe genres and sounds, never artists.",
            Schema = ToolSchema.Of(
                Param.Text("name", 1, 60),
                Param.Choice("genreFamily", Genres),
                Param.Text("subGenre", 1, 40).Optional(),
                Param.Choice("tempoFeel", "keep", "laidback", "driving", "halfTime", "swing"),
                Param.List("instrumentation", Tag, 0, ContractLimits.StyleMaxTags).Optional(),
                Param.List("moods", Tag, 0, ContractLimits.StyleMaxTags).Optional(),
                Param.Choice("vocalHandling", "keep", "instrumental"),
                Param.Choice("defaultStrength", Strengths),
                Param.Text("description", 1, 300).Optional()),
            UndoKind = UndoKindDto.Compensate,
            ImitationArguments = ["name", "subGenre", "instrumentation", "moods", "description"],
            Handler = CreateStyleAsync,
        },
        Create("delete_style", ToolRiskDto.Destructive) with
        {
            Title = "Delete a style",
            Description = "Delete an own style (presets cannot be deleted). Generations keep their copy. The user must confirm.",
            Schema = ToolSchema.Of(Param.Uuid("styleId")),
            TargetArgument = "styleId",
            Handler = async (call, cancellationToken) =>
            {
                await call.Actions.Styles.DeleteStyleAsync(call.RequiredId("styleId"), cancellationToken);
                return ToolResult.Of(new JsonObject { ["deleted"] = true });
            },
        },
        Create("restyle_track", ToolRiskDto.Heavy) with
        {
            Title = "Restyle a track",
            Description = "Render a part of a track (10-480 s, default range mode) in another style, chosen by styleId from list_styles. GPU job: the user must confirm. Wait with wait_for_job phase restyle and the generationId.",
            Schema = RestyleSchema(),
            UndoKind = UndoKindDto.Compensate,
            TargetArgument = "trackId",
            Handler = RestyleAsync,
            Estimator = (call, _) => Task.FromResult(RestyleEstimate(call)),
        },
        Create("list_generations", ToolRiskDto.Read) with
        {
            Title = "List generations",
            Description = "List restyles, songs and voice conversions (newest first, at most 20) with status, progress and takes. A generated song is found here by its title, not with search_library.",
            Schema = ToolSchema.Of(Param.Choice("kind", "restyle", "song", "voiceConversion").Optional(), Param.Uuid("trackId").Optional()),
            Handler = ListGenerationsAsync,
        },
        Create("cancel_generation", ToolRiskDto.Change) with
        {
            Title = "Cancel a generation",
            Description = "Cancel a queued or running generation.",
            Schema = ToolSchema.Of(GenerationId),
            Handler = async (call, cancellationToken) => ToolResult.Of(new JsonObject { ["status"] = Name((await call.Actions.Generations.CancelGenerationAsync(call.RequiredId("generationId"), cancellationToken)).Status) }),
        },
        Create("retry_generation", ToolRiskDto.Heavy) with
        {
            Title = "Retry a generation",
            Description = "Run a failed or cancelled generation again. GPU job: the user must confirm.",
            Schema = ToolSchema.Of(GenerationId),
            UndoKind = UndoKindDto.Inverse,
            TargetArgument = "generationId",
            Handler = RetryAsync,
        },
        Create("import_take", ToolRiskDto.Job) with
        {
            Title = "Import a take as a track",
            Description = "Add take 1 or 2 of a finished generation to the library as a new track (it is separated into stems).",
            Schema = ToolSchema.Of(GenerationId, Param.Whole("index", 1, 2)),
            UndoKind = UndoKindDto.Compensate,
            Handler = ImportAsync,
        },
        Create("delete_generation", ToolRiskDto.Destructive) with
        {
            Title = "Delete a generation",
            Description = "Delete a generation and its takes. Cannot be undone: the user must confirm.",
            Schema = ToolSchema.Of(GenerationId),
            TargetArgument = "generationId",
            Handler = async (call, cancellationToken) =>
            {
                await call.Actions.Generations.DeleteGenerationAsync(call.RequiredId("generationId"), cancellationToken);
                return ToolResult.Of(new JsonObject { ["deleted"] = true });
            },
        },
    ];

    private static AgentToolDescriptor Create(string name, ToolRiskDto risk) =>
        Tool(F, name, ToolKindDto.Server, risk) with { Group = ToolGroup.Create, Availability = ToolAvailability.GenerationService };

    private static async Task<ToolResult> StatusAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var status = await call.Actions.Generations.GetStatusAsync(cancellationToken);
        return ToolResult.Of(new JsonObject
        {
            ["state"] = Name(status.State),
            ["reason"] = StatusReason(status.State),
            ["device"] = Device(status.Device),
            ["voiceState"] = Name(status.Voice.State),
            ["downloadBytesNeeded"] = status.DownloadBytesNeeded,
        });
    }

    private static string? StatusReason(GenerationAvailabilityDto state) => state switch
    {
        GenerationAvailabilityDto.Ready => null,
        GenerationAvailabilityDto.NotInstalled => "The generator is not installed.",
        _ => "The generator is not available.",
    };

    private static JsonObject? Device(Contracts.Health.ProcessingDeviceDto? device) =>
        device is { Kind: { } kind } ? new JsonObject { ["kind"] = Name(kind), ["name"] = TrustedText.SafeName(device.Name) } : null;

    private static async Task<ToolResult> ListStylesAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var query = new StyleQuery { Query = call.Text("query"), Kind = Parse<StyleKindDto>(call.Text("kind")), GenreFamily = Parse<GenreFamilyDto>(call.Text("genreFamily")) };
        var styles = await call.Actions.Styles.ListStylesAsync(query, cancellationToken);
        return ToolResult.Of(new JsonArray([.. styles.Take(MaxStyles).Select(style => (JsonNode)StyleRow(style))]));
    }

    private static JsonObject StyleRow(StyleDto style)
    {
        var row = new JsonObject { ["styleId"] = style.Id.ToString(), ["kind"] = Name(style.Kind) };
        row[style.Kind == StyleKindDto.Preset ? "name" : ResultProjector.UntrustedField] = style.Kind == StyleKindDto.Preset ? style.Name : Untrusted(style.Name);
        row["genreFamily"] = Name(style.GenreFamily);
        row["tempoFeel"] = Name(style.TempoFeel);
        row["vocalHandling"] = Name(style.VocalHandling);
        row["defaultStrength"] = Name(style.DefaultStrength);
        return row;
    }

    private static async Task<ToolResult> CreateStyleAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var input = StyleInputOf(call);
        var style = await call.Actions.Styles.CreateStyleAsync(input, cancellationToken);
        return new ToolResult
        {
            Projection = new JsonObject { ["styleId"] = style.Id.ToString(), ["name"] = input.Name },
            Undo = Undo("delete_style", new JsonObject { ["styleId"] = style.Id.ToString() }),
            UndoFresh = true,
        };
    }

    private static StyleInput StyleInputOf(ToolInvocation call) => new()
    {
        Name = call.Text("name")!,
        GenreFamily = Parse<GenreFamilyDto>(call.Text("genreFamily"))!.Value,
        SubGenre = call.Text("subGenre"),
        TempoFeel = Parse<TempoFeelDto>(call.Text("tempoFeel"))!.Value,
        Instrumentation = call.Strings("instrumentation"),
        Moods = call.Strings("moods"),
        VocalHandling = Parse<VocalHandlingDto>(call.Text("vocalHandling"))!.Value,
        DefaultStrength = Parse<StrengthDto>(call.Text("defaultStrength"))!.Value,
        Description = call.Text("description"),
    };

    private static ToolSchema RestyleSchema() => ToolSchema.Of(
        Param.Uuid("trackId"),
        Param.Uuid("styleId").Describe("Style id from list_styles; never a name."),
        Param.Choice("mode", "range", "inPlace").Optional(),
        Param.Number("rangeStartSeconds", 0, 36_000).Optional(),
        Param.Number("rangeEndSeconds", 0, 36_000).Optional(),
        Param.List("includedStems", Param.Choice("stem", "vocals", "drums", "bass", "guitar", "piano", "other"), 1, 6).Optional(),
        Param.Choice("strength", Strengths).Optional(),
        Param.Whole("takes", 1, 2).Optional());

    private static ToolEstimate RestyleEstimate(ToolInvocation call)
    {
        var span = call.Number("rangeEndSeconds") is { } end && call.Number("rangeStartSeconds") is { } start ? end - start : (double?)null;
        return Estimate("Restyle a part of a track", span, call.Whole("takes") ?? 1);
    }

    private static async Task<ToolResult> RestyleAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var trackId = call.RequiredId("trackId");
        var generation = await call.Actions.Generations.CreateRestyleAsync(trackId, RestyleRequestOf(call), cancellationToken);
        return Started(generation, JobPhaseDto.Restyle, RestyleEstimate(call).EstimatedMinutes, trackId);
    }

    private static RestyleRequest RestyleRequestOf(ToolInvocation call) => new()
    {
        StyleId = call.RequiredId("styleId"),
        Mode = Parse<RestyleModeDto>(call.Text("mode")) ?? RestyleModeDto.Range,
        RangeStartSeconds = call.Number("rangeStartSeconds"),
        RangeEndSeconds = call.Number("rangeEndSeconds"),
        IncludedStems = call.Strings("includedStems") is { Count: > 0 } stems ? stems : null,
        Strength = Parse<StrengthDto>(call.Text("strength")),
        Takes = call.Whole("takes") ?? 1,
    };

    private static async Task<ToolResult> ListGenerationsAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var query = new GenerationQuery { Kind = Parse<GenerationKindDto>(call.Text("kind")), SourceTrackId = call.Id("trackId") };
        var generations = await call.Actions.Generations.ListGenerationsAsync(query, cancellationToken);
        return ToolResult.Of(new JsonArray([.. generations.Take(MaxGenerations).Select(generation => (JsonNode)Row(generation))]));
    }

    private static async Task<ToolResult> RetryAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var generation = await call.Actions.Generations.RetryGenerationAsync(call.RequiredId("generationId"), cancellationToken);
        var phase = generation.Kind == GenerationKindDto.Restyle ? JobPhaseDto.Restyle : JobPhaseDto.Generation;
        return Started(generation, phase, null) with { Undo = Undo("cancel_generation", new JsonObject { ["generationId"] = generation.Id.ToString() }) };
    }

    private static async Task<ToolResult> ImportAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var track = await call.Actions.Generations.ImportTakeAsync(call.RequiredId("generationId"), call.Whole("index")!.Value, cancellationToken);
        var job = new Model.JobRef { Phase = JobPhaseDto.Standard, TrackId = track.Id };
        return new ToolResult
        {
            Projection = new JsonObject { ["trackId"] = track.Id.ToString(), ["status"] = Name(track.Status), ["jobRef"] = JobRef(job) },
            Job = job,
            Undo = Undo("delete_track", new JsonObject { ["trackId"] = track.Id.ToString() }),
        };
    }
}
