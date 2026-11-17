using System.Net;
using System.Text;
using System.Text.Json;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>
/// Contract-shaped lyric-assistant data (backend-api 1.23.0): the contract samples of <c>LyricAssistSamples.json</c> (read from the
/// repository, so a contract change breaks these fakes) and builders with the same ids and shapes. All texts are self-written.
/// </summary>
public static class LyricAssistData
{
    /// <summary>The sheet id of the contract samples.</summary>
    public static readonly Guid LyricsId = Guid.Parse("2c4e6a8b-0d1f-4a3b-8c5d-7e9f1a3b5c7d");

    /// <summary>The assist id of the contract samples.</summary>
    public static readonly Guid AssistId = Guid.Parse("5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d");

    /// <summary>The matched library track of the not-own refusal.</summary>
    public static readonly Guid TrackId = Guid.Parse("7b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d4e");

    /// <summary>The spelling suggestion on "wiet" (line 2).</summary>
    public static readonly Guid Spelling = Guid.Parse("11111111-1111-4111-8111-111111111111");

    /// <summary>The grammar suggestion on line 3.</summary>
    public static readonly Guid Grammar = Guid.Parse("22222222-2222-4222-8222-222222222222");

    /// <summary>The device suggestion on line 3.</summary>
    public static readonly Guid Device = Guid.Parse("33333333-3333-4333-8333-333333333333");

    /// <summary>A self-written Dutch sheet: a marker and three lines.</summary>
    public const string Text = "[verse]\nIk wiet niet waar de wind gaat\nDe nacht zijn warm en stil";

    /// <summary>JSON options of the contract (camelCase).</summary>
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset At = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The JSON samples of the contract (keys <c>suggestion</c>, <c>summary</c>, <c>assist</c>).</summary>
    /// <param name="key">The sample.</param>
    /// <returns>The JSON text.</returns>
    public static string Sample(string key)
    {
        var path = Path.Combine(RepoPaths.Root, "tests", "StemStudio.Api.Tests", "Contract", "LyricAssistSamples.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return JsonSerializer.Serialize(document.RootElement.GetProperty(key));
    }

    /// <summary>A saved sheet.</summary>
    public static LyricsSheetDto Sheet(string language = "nl", string text = Text, int revision = 3, SheetAssistInfoDto? assist = null) => new()
    {
        Id = LyricsId,
        Title = "Zomeravond",
        Language = language,
        Text = text,
        CreatedAt = At,
        UpdatedAt = At,
        Revision = revision,
        Assist = assist ?? SheetAssistInfoDto.Default,
    };

    /// <summary>The status: dictionary available, the model ready (Ollama) unless told otherwise.</summary>
    public static LyricAssistStatusDto Status(AgentBackendDto backend = AgentBackendDto.Ollama, bool ready = true, bool notice = false, string? qualityNote = null) => new()
    {
        Languages = [AssistLanguageDto.Nl, AssistLanguageDto.En],
        Dictionary = Layer(SuggestionSourceDto.Dictionary, "OpenTaal dictionary"),
        Model = new LyricAssistModelStatusDto
        {
            Backend = backend,
            Ready = ready,
            ActiveModel = ready ? "qwen3.5:9b" : null,
            UsingFallback = false,
            Reason = ready ? null : "The AI model is switched off in Settings.",
            Fix = ready ? null : "Choose a model backend in Settings > AI assistant.",
            QualityNote = qualityNote,
        },
        ClaudeNoticeRequired = notice,
        AssistActive = null,
    };

    /// <summary>The settings with the notice and the own-work statement.</summary>
    public static LyricAssistSettingsDto Settings() => new()
    {
        ClaudeNoticeAcknowledged = false,
        ClaudeNoticeVersion = LyricAssistLimits.ClaudeNoticeVersion,
        ClaudeNoticeText = "Your lyrics text is sent to Anthropic for this check; paid per use.",
        OwnWorkStatementVersion = LyricAssistLimits.OwnWorkStatementVersion,
        OwnWorkStatementText = "I confirm that I wrote this text myself.",
    };

    /// <summary>A layer line.</summary>
    public static LayerInfoDto Layer(SuggestionSourceDto layer, string name, bool available = true) =>
        new() { Layer = layer, Name = name, Available = available };

    /// <summary>The spelling suggestion "wiet" -> "weet" on line 2.</summary>
    public static LyricSuggestionDto SpellingFix(SuggestionStatusDto status = SuggestionStatusDto.Open) =>
        Suggestion(Spelling, SuggestionTypeDto.SpellingTypo, new AnchorDto { Line = 2, Start = 3, Length = 4, Original = "wiet" }, "weet") with
        {
            Severity = SuggestionSeverityDto.Error,
            Source = SuggestionSourceDto.Dictionary,
            Model = null,
            Status = status,
        };

    /// <summary>The grammar suggestion "zijn" -> "is" on line 3.</summary>
    public static LyricSuggestionDto GrammarFix() =>
        Suggestion(Grammar, SuggestionTypeDto.GrammarAgreement, new AnchorDto { Line = 3, Start = 9, Length = 4, Original = "zijn" }, "is");

    /// <summary>The end-rhyme idea "warm" -> "zacht" on line 3 with syllables and the rhyme partner.</summary>
    public static LyricSuggestionDto DeviceIdea() =>
        Suggestion(Device, SuggestionTypeDto.DeviceEndRhyme, new AnchorDto { Line = 3, Start = 14, Length = 4, Original = "warm" }, "zacht") with
        {
            Severity = SuggestionSeverityDto.Idea,
            SyllablesBefore = 7,
            SyllablesAfter = 6,
            RhymeWith = new RhymePartnerDto { Line = 2, Word = "gaat" },
        };

    /// <summary>A model suggestion.</summary>
    public static LyricSuggestionDto Suggestion(Guid id, SuggestionTypeDto type, AnchorDto anchor, string? proposal) => new()
    {
        SuggestionId = id,
        AssistId = AssistId,
        Category = CategoryOf(type),
        Type = type,
        Severity = SuggestionSeverityDto.Improvement,
        Anchor = anchor,
        Operation = SuggestionOperationDto.Replace,
        Proposal = proposal,
        Explanation = "Uitleg bij de suggestie.",
        Source = SuggestionSourceDto.Model,
        Model = "qwen3.5:9b",
        Status = SuggestionStatusDto.Open,
    };

    /// <summary>The category of a type (its wire prefix; <c>write.*</c> maps to the writing-help categories).</summary>
    public static SuggestionCategoryDto CategoryOf(SuggestionTypeDto type) => JsonSerializer.Serialize(type).Trim('"') switch
    {
        var t when t.StartsWith("write.alternative", StringComparison.Ordinal) => SuggestionCategoryDto.Alternative,
        var t when t.StartsWith("write.continuation", StringComparison.Ordinal) => SuggestionCategoryDto.Continuation,
        var t when t.StartsWith("write.", StringComparison.Ordinal) => SuggestionCategoryDto.Title,
        var t => JsonSerializer.Deserialize<SuggestionCategoryDto>($"\"{t.Split('.')[0]}\""),
    };

    /// <summary>An assist snapshot.</summary>
    public static LyricAssistDto Assist(AssistStatusDto status = AssistStatusDto.Running, AssistKindDto kind = AssistKindDto.Check, params LyricSuggestionDto[] suggestions) => new()
    {
        AssistId = AssistId,
        LyricsId = LyricsId,
        SheetRevision = 3,
        Kind = kind,
        Status = status,
        Language = AssistLanguageDto.Nl,
        Sensitivity = SensitivityDto.Balanced,
        Layers = [Layer(SuggestionSourceDto.Dictionary, "OpenTaal dictionary"), Layer(SuggestionSourceDto.Model, "qwen3.5:9b, local")],
        Backend = AgentBackendDto.Ollama,
        Model = "qwen3.5:9b",
        UsingFallback = false,
        BlocksTotal = 1,
        BlocksDone = status == AssistStatusDto.Completed ? 1 : 0,
        BlocksSkipped = 0,
        Counts = CategoryCountsDto.None,
        Discarded = DiscardedCountsDto.None,
        Origin = AssistOriginDto.Editor,
        CreatedAt = At,
        Suggestions = suggestions,
    };

    /// <summary>An accept (or undo) result.</summary>
    public static AcceptResultDto Accepted(string text, int revision = 4, Guid[]? applied = null, Guid[]? stale = null) => new()
    {
        LyricsId = LyricsId,
        Revision = revision,
        Title = "Zomeravond",
        Text = text,
        Assist = SheetAssistInfoDto.Default with { CanUndo = true, AssistedLines = [new AssistedLineDto { Line = 2, SuggestionId = Spelling }] },
        Applied = applied ?? [Spelling],
        Stale = stale ?? [],
    };

    /// <summary>A suggestion list.</summary>
    public static SuggestionListDto List(params LyricSuggestionDto[] suggestions) => new() { LyricsId = LyricsId, Revision = 3, Suggestions = suggestions };

    /// <summary>An <c>AssistProblem</c> as the client throws it.</summary>
    /// <param name="code">The problem code.</param>
    /// <param name="extensionsJson">Extension members as a JSON object.</param>
    /// <param name="status">The status.</param>
    /// <returns>The exception.</returns>
    public static ApiException Problem(string code, string extensionsJson = "{}", HttpStatusCode status = HttpStatusCode.Conflict)
    {
        using var document = JsonDocument.Parse(extensionsJson);
        var extensions = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        return new ApiException(new ApiError(status, $"Problem {code}", null, new Dictionary<string, string[]>())
        {
            Type = LyricAssistProblemTypes.Uri(code),
            Extensions = extensions,
        });
    }

    /// <summary>The not-own-lyrics refusal naming the fake track.</summary>
    public static ApiException NotOwn(bool overridable = true) => Problem(
        LyricAssistProblemTypes.NotOwnLyrics,
        $$"""{"code":"not-own-lyrics","matchedTrackTitle":"Fake Song","matchedTrackId":"{{TrackId}}","overridable":{{(overridable ? "true" : "false")}}}""");

    /// <summary>An SSE body of events (<c>(type, json)</c>), numbered from 1.</summary>
    public static string Sse(params (string Type, string Json)[] events)
    {
        var text = new StringBuilder(": keep-alive\n\n");
        for (var i = 0; i < events.Length; i++)
        {
            text.Append(System.Globalization.CultureInfo.InvariantCulture, $"id: {i + 1}\nevent: {events[i].Type}\ndata: {events[i].Json}\n\n");
        }

        return text.ToString();
    }

    /// <summary>Events as the client yields them.</summary>
    public static async IAsyncEnumerable<StemStudio.Web.Components.Lyrics.Assist.LyricAssistEvent> Events(params (string Type, object Data)[] events)
    {
        await Task.Yield();
        for (var i = 0; i < events.Length; i++)
        {
            yield return new(i + 1, events[i].Type, JsonSerializer.SerializeToElement(events[i].Data, Web));
        }
    }

    /// <summary>The completed event with counts.</summary>
    public static LyricAssistCompletedEventDto Completed(int spelling = 1, int grammar = 0, int verbatim = 0) => new()
    {
        Counts = CategoryCountsDto.None with { Spelling = spelling, Grammar = grammar },
        Discarded = DiscardedCountsDto.None with { Verbatim = verbatim },
        DurationMs = 1200,
    };
}
