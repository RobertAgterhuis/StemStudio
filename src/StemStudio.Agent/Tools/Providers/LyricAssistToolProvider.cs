using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using static StemStudio.Agent.Tools.Providers.GenerationTools;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>
/// Tools of feature 023 (tool catalogue 1.2.0, <see cref="LyricAssistToolNames"/>, backend API 1.23.0). Every result is text-free
/// (FR-019): ids, line numbers, categories, types, counts and statuses only - sheet and suggestion text never reach the agent's
/// model. Applying suggestions always needs the user's approval (<c>lyrics-edit</c>, FR-018, FR-020) and takes ids of open stored
/// suggestions only; instructions pass the artist-imitation guardrail; <c>find_rhymes</c> needs a word the user named.
/// </summary>
public sealed class LyricAssistToolProvider : IAgentToolProvider
{
    /// <summary>Undo-only tool of apply_lyric_suggestions (<c>POST /api/lyrics/{id}/undo</c>).</summary>
    public const string UndoAcceptTool = "undo_lyric_suggestions";

    /// <summary>Undo-only tool of mark_lyrics_intentional (<c>DELETE .../intentional-marks/{markId}</c>).</summary>
    public const string DeleteMarkTool = "delete_intentional_mark";

    /// <summary>Refusal of the apply pre-check when an id is not an open suggestion of the sheet.</summary>
    public const string NotOpen = "Only open suggestions of this lyrics sheet can be applied; list them with list_lyric_suggestions first.";

    private const string F = "023";
    private const int MaxSuggestions = 50;
    private const int MaxRhymes = 30;
    private const int MaxLines = 120;

    private static readonly ToolParameter LyricsId = Param.Uuid("lyricsId").Describe("Lyrics sheet id (the open sheet of the page, or from list_lyrics_sheets).");
    private static readonly ToolParameter Line = Param.Whole("line", 1, 4096);
    private static readonly ToolParameter Instruction = Param.Text("instruction", 1, LyricAssistLimits.InstructionMaxLength)
        .Describe("Mood or theme wish (no artists).");

    private static readonly string[] Categories = ["spelling", "grammar", "flow", "licence", "device"];
    private static readonly string[] Statuses = ["open", "accepted", "rejected", "intentional", "stale", "superseded"];
    private static readonly string[] AllCategories = ["spelling", "grammar", "flow", "licence", "device", "alternative", "continuation", "title"];

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        Create(LyricAssistToolNames.CheckLyrics, ToolRiskDto.Job) with
        {
            Title = "Check lyrics",
            Description = "Check a lyrics sheet for spelling, grammar and flow (mode check) or suggest poetic devices (mode devices). The suggestions appear in the lyrics editor for the user to review; this tool never sees or changes the text. Wait with wait_for_job phase lyricAssist, then use list_lyric_suggestions for counts.",
            Schema = ToolSchema.Of(
                LyricsId,
                Param.Choice("mode", "check", "devices").Optional(),
                Param.List("categories", Param.Choice("category", Categories), 1, LyricAssistLimits.MaxCategories).Optional(),
                Param.Whole("lineFrom", 1, 4096).Optional(),
                Param.Whole("lineTo", 1, 4096).Optional(),
                Instruction.Optional()),
            ImitationArguments = ["instruction"],
            TargetArgument = "lyricsId",
            Handler = CheckAsync,
        },
        Create(LyricAssistToolNames.SuggestLyricLines, ToolRiskDto.Job) with
        {
            Title = "Suggest lyric lines",
            Description = "Ask the lyric assistant for 1-5 alternatives of a line (mode alternatives, line), a continuation of 1-4 lines after a line (mode continue, line) or up to 5 title ideas (mode titles). Results are suggestions in the lyrics editor; nothing changes until the user accepts. Wait with wait_for_job phase lyricAssist.",
            Schema = ToolSchema.Of(LyricsId, Param.Choice("mode", "alternatives", "continue", "titles"), Line.Optional(), Param.Whole("count", 1, 5).Optional(), Instruction.Optional()),
            Availability = ToolAvailability.LyricModelService,
            ImitationArguments = ["instruction"],
            TargetArgument = "lyricsId",
            Handler = SuggestAsync,
        },
        Create(LyricAssistToolNames.ListLyricSuggestions, ToolRiskDto.Read) with
        {
            Title = "List lyric suggestions",
            Description = "List the suggestions of a lyrics sheet as ids, lines, categories, types, severities and statuses (no text). Default status open.",
            Schema = ToolSchema.Of(LyricsId, Param.Choice("status", Statuses).Optional(), Param.Choice("category", AllCategories).Optional()),
            Handler = ListAsync,
        },
        Create(LyricAssistToolNames.ApplyLyricSuggestions, ToolRiskDto.Change) with
        {
            Title = "Apply lyric suggestions",
            Description = "Apply open suggestions of a lyrics sheet by id (from list_lyric_suggestions). The user always confirms every change first. Undoable.",
            Schema = ToolSchema.Of(LyricsId, Param.List("suggestionIds", Param.Uuid("suggestionId"), 1, MaxSuggestions)),
            UndoKind = UndoKindDto.Inverse,
            AlwaysApprovalReason = AgentDecisionReasons.LyricsEdit,
            TargetArgument = "lyricsId",
            Precondition = OpenSuggestionsAsync,
            Handler = ApplyAsync,
        },
        Create(LyricAssistToolNames.MarkLyricsIntentional, ToolRiskDto.Change) with
        {
            Title = "Mark lyrics as intentional",
            Description = "Mark the text of a suggestion as intentional (word, phrase or line), so the assistant never flags it again. Undoable.",
            Schema = ToolSchema.Of(LyricsId, Param.Uuid("suggestionId"), Param.Choice("scope", "word", "phrase", "line")),
            UndoKind = UndoKindDto.Inverse,
            TargetArgument = "lyricsId",
            Handler = MarkAsync,
        },
        Create(LyricAssistToolNames.FindRhymes, ToolRiskDto.Read) with
        {
            Title = "Find rhymes",
            Description = "Perfect and near rhymes from the local dictionaries for a word the user named, in Dutch (nl) or English (en).",
            Schema = ToolSchema.Of(Param.Text("word", 1, LyricAssistLimits.WordMaxLength), Param.Choice("language", "nl", "en"), Param.Whole("limit", 1, MaxRhymes).Optional()),
            UserWordArguments = ["word"],
            Handler = RhymesAsync,
        },
        Create(LyricAssistToolNames.CountSyllables, ToolRiskDto.Read) with
        {
            Title = "Count syllables",
            Description = "Syllables per line of a lyrics sheet with the section medians and the deviations from the rhythm target (numbers only).",
            Schema = ToolSchema.Of(LyricsId),
            Handler = SyllablesAsync,
        },
        Tool(F, LyricAssistToolNames.OpenLyricsEditor, ToolKindDto.Ui, ToolRiskDto.Read) with
        {
            Group = ToolGroup.LyricAssist,
            Title = "Open the lyrics editor",
            Description = "Open a lyrics sheet in the editor, optionally with the assistant pane on an assist (assistId), so the user can review suggestions.",
            Schema = ToolSchema.Of(LyricsId, Param.Uuid("assistId").Optional()),
        },
    ];

    /// <summary>The undo-only tools of feature 023 (registered with <see cref="UndoTools"/>).</summary>
    /// <returns>The descriptors.</returns>
    internal static IReadOnlyList<AgentToolDescriptor> UndoOnly() =>
    [
        Tool("011", UndoAcceptTool, ToolKindDto.Server, ToolRiskDto.Change) with
        {
            Title = "Undo applied lyric suggestions",
            Description = "Undo of apply_lyric_suggestions.",
            Schema = ToolSchema.Of(LyricsId, Param.Whole("revision", 1, int.MaxValue)),
            Handler = UndoAcceptAsync,
        },
        Tool("011", DeleteMarkTool, ToolKindDto.Server, ToolRiskDto.Change) with
        {
            Title = "Remove an intentional mark",
            Description = "Undo of mark_lyrics_intentional.",
            Schema = ToolSchema.Of(LyricsId, Param.Uuid("markId")),
            Handler = DeleteMarkAsync,
        },
    ];

    private static AgentToolDescriptor Create(string name, ToolRiskDto risk) => Tool(F, name, ToolKindDto.Server, risk) with { Group = ToolGroup.LyricAssist };

    private static async Task<ToolResult> CheckAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var devices = call.Text("mode") == "devices";
        if (!devices && call.Text("instruction") is not null)
        {
            throw new AgentActionException("An instruction is only used with mode devices.");
        }

        var request = new CreateLyricAssistRequest
        {
            Kind = devices ? AssistKindDto.Devices : AssistKindDto.Check,
            ExpectedRevision = 0,
            Categories = call.Strings("categories") is { Count: > 0 } names ? [.. names.Select(name => Parse<SuggestionCategoryDto>(name)!.Value)] : null,
            LineFrom = call.Whole("lineFrom"),
            LineTo = call.Whole("lineTo"),
            Instruction = call.Text("instruction"),
        };
        return await StartAsync(call, request, cancellationToken);
    }

    private static async Task<ToolResult> SuggestAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var kind = Parse<AssistKindDto>(call.Text("mode"))!.Value;
        var line = call.Whole("line");
        if (kind != AssistKindDto.Titles && line is null)
        {
            throw new AgentActionException("Give the line number for alternatives and continue.");
        }

        if (kind == AssistKindDto.Continue && call.Whole("count") > LyricAssistLimits.MaxContinuationLines)
        {
            throw new AgentActionException("A continuation has at most 4 lines.");
        }

        var request = new CreateLyricAssistRequest
        {
            Kind = kind,
            ExpectedRevision = 0,
            LineFrom = kind == AssistKindDto.Titles ? null : line,
            LineTo = kind == AssistKindDto.Titles ? null : line,
            Count = call.Whole("count"),
            Instruction = call.Text("instruction"),
        };
        return await StartAsync(call, request, cancellationToken);
    }

    /// <summary>Reads the sheet's revision first (GET /api/lyrics/{id}), then starts the assist on it.</summary>
    private static async Task<ToolResult> StartAsync(ToolInvocation call, CreateLyricAssistRequest request, CancellationToken cancellationToken)
    {
        var actions = call.LyricActions();
        var lyricsId = call.RequiredId("lyricsId");
        var sheet = await actions.GetSheetStateAsync(lyricsId, cancellationToken);
        var assist = await actions.StartAssistAsync(lyricsId, request with { ExpectedRevision = sheet.Revision, Origin = AssistOriginDto.Agent, AgentRunId = call.RunId }, cancellationToken);
        var job = new JobRef { JobId = assist.AssistId.ToString(), Phase = JobPhaseDto.LyricAssist };
        return new ToolResult
        {
            Projection = new JsonObject
            {
                ["assistId"] = assist.AssistId.ToString(),
                ["status"] = Name(assist.Status),
                ["language"] = Name(assist.Language),
                ["layers"] = new JsonArray([.. assist.Layers.Select(layer => (JsonNode)new JsonObject
                {
                    ["layer"] = Name(layer.Layer),
                    ["name"] = TrustedText.LayerName(layer.Name),
                    ["available"] = layer.Available,
                })]),
                ["jobRef"] = JobRef(job),
            },
            Job = job,
            EstimatedMinutes = assist.EstimatedSeconds is { } seconds ? Math.Round(seconds / 60, 1) : null,
        };
    }

    private static async Task<ToolResult> ListAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var status = Parse<SuggestionStatusDto>(call.Text("status")) ?? SuggestionStatusDto.Open;
        var summary = await call.LyricActions().ListSuggestionsAsync(call.RequiredId("lyricsId"), status, Parse<SuggestionCategoryDto>(call.Text("category")), cancellationToken);
        var counts = new JsonObject();
        foreach (var group in summary.Items.GroupBy(item => item.Category).OrderBy(group => group.Key))
        {
            counts[Name(group.Key)] = group.Count();
        }

        return ToolResult.Of(new JsonObject
        {
            ["revision"] = summary.Revision,
            ["counts"] = counts,
            ["items"] = new JsonArray([.. summary.Items.Take(MaxSuggestions).Select(item => (JsonNode)new JsonObject
            {
                ["suggestionId"] = item.SuggestionId.ToString(),
                ["category"] = Name(item.Category),
                ["type"] = Name(item.Type),
                ["severity"] = Name(item.Severity),
                ["line"] = item.Line,
                ["status"] = Name(item.Status),
                ["source"] = Name(item.Source),
            })]),
        });
    }

    /// <summary>Policy pre-check (tool catalogue 1.2.0): every id must be an open suggestion of the sheet.</summary>
    private static async Task OpenSuggestionsAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var open = await call.LyricActions().ListSuggestionsAsync(call.RequiredId("lyricsId"), SuggestionStatusDto.Open, null, cancellationToken);
        var ids = open.Items.Select(item => item.SuggestionId).ToHashSet();
        if (!call.Strings("suggestionIds").All(id => Guid.TryParse(id, out var guid) && ids.Contains(guid)))
        {
            throw new AgentActionException(NotOpen);
        }
    }

    private static async Task<ToolResult> ApplyAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        await OpenSuggestionsAsync(call, cancellationToken);
        var actions = call.LyricActions();
        var lyricsId = call.RequiredId("lyricsId");
        var sheet = await actions.GetSheetStateAsync(lyricsId, cancellationToken);
        var ids = call.Strings("suggestionIds").Select(Guid.Parse).Distinct().ToList();
        var result = await actions.AcceptAsync(lyricsId, new SuggestionDecisionRequest { SuggestionIds = ids, ExpectedRevision = sheet.Revision }, cancellationToken);
        return new ToolResult
        {
            Projection = Accept(result),
            Undo = Undo(UndoAcceptTool, new JsonObject { ["lyricsId"] = lyricsId.ToString(), ["revision"] = result.Revision }),
        };
    }

    private static async Task<ToolResult> MarkAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var actions = call.LyricActions();
        var lyricsId = call.RequiredId("lyricsId");
        var sheet = await actions.GetSheetStateAsync(lyricsId, cancellationToken);
        var request = new MarkSuggestionIntentionalRequest { Scope = Parse<IntentionalScopeDto>(call.Text("scope"))!.Value, ExpectedRevision = sheet.Revision };
        var mark = await actions.MarkIntentionalAsync(lyricsId, call.RequiredId("suggestionId"), request, cancellationToken);
        return new ToolResult
        {
            Projection = new JsonObject { ["markId"] = mark.MarkId.ToString(), ["scope"] = Name(mark.Scope) },
            Undo = Undo(DeleteMarkTool, new JsonObject { ["lyricsId"] = lyricsId.ToString(), ["markId"] = mark.MarkId.ToString() }),
        };
    }

    private static async Task<ToolResult> RhymesAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var language = Parse<AssistLanguageDto>(call.Text("language"))!.Value;
        var result = await call.LyricActions().FindRhymesAsync(call.Text("word")!.Trim(), language, call.Whole("limit"), cancellationToken);
        return ToolResult.Of(new JsonObject
        {
            ["rhymes"] = new JsonArray([.. result.Rhymes.Take(MaxRhymes).Select(rhyme => (JsonNode)new JsonObject
            {
                ["word"] = ResultProjector.Untrusted(rhyme.Word, LyricAssistLimits.WordMaxLength),
                ["kind"] = Name(rhyme.Kind),
                ["syllables"] = rhyme.Syllables,
            })]),
            ["source"] = TrustedText.SafeName(result.Source),
        });
    }

    private static async Task<ToolResult> SyllablesAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var report = await call.LyricActions().CountSyllablesAsync(call.RequiredId("lyricsId"), cancellationToken);
        var medians = new JsonObject();
        foreach (var line in report.Lines.Where(line => line.SectionMedian is not null && TrustedText.SafeName(line.Section) is not null))
        {
            medians[line.Section!] = line.SectionMedian;
        }

        return ToolResult.Of(new JsonObject
        {
            ["lines"] = new JsonArray([.. report.Lines.Where(line => !line.IsMarker).Take(MaxLines).Select(line => (JsonNode)new JsonObject
            {
                ["line"] = line.Line,
                ["syllables"] = line.Syllables,
                ["target"] = line.Target,
                ["deviates"] = line.Deviates,
            })]),
            ["sectionMedians"] = medians,
            ["wordsPerSecond"] = report.WordsPerSecond,
        });
    }

    private static async Task<ToolResult> UndoAcceptAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var result = await call.LyricActions().UndoAsync(call.RequiredId("lyricsId"), call.Whole("revision")!.Value, cancellationToken);
        return ToolResult.Of(Accept(result));
    }

    private static async Task<ToolResult> DeleteMarkAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        await call.LyricActions().DeleteIntentionalMarkAsync(call.RequiredId("lyricsId"), call.RequiredId("markId"), cancellationToken);
        return ToolResult.Of(new JsonObject { ["deleted"] = true });
    }

    private static JsonObject Accept(LyricAcceptSummary result) => new() { ["revision"] = result.Revision, ["applied"] = result.Applied, ["stale"] = result.Stale };
}
