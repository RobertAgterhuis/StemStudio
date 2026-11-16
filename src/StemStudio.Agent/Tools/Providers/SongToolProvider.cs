using System.Text.Json.Nodes;
using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using static StemStudio.Agent.Tools.Providers.GenerationTools;
using static StemStudio.Agent.Tools.Providers.ToolDefinitions;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>
/// The own-lyrics-to-song and own-voice tools of feature 010 (tool catalogue 1.0.0). Lyrics text reaches a song only when the
/// user typed it (<c>lyrics-not-from-user</c>); saved sheets are referenced by id and their text never passes the model. The
/// agent never creates, deletes or consents to a voice profile; it only lists and uses existing ones (FR-025, FR-026).
/// </summary>
public sealed class SongToolProvider : IAgentToolProvider
{
    private const string F = "010";
    private const int MaxSheets = 20;

    /// <summary>Refusal when the profile does not exist (no personal data).</summary>
    public const string UnknownProfile = "This voice profile does not exist; choose one from list_voice_profiles.";

    /// <summary>Refusal when the consent is not current (no personal data).</summary>
    public const string ConsentNotCurrent = "Renew the consent for this voice on the Voices page";
    private static readonly ToolParameter Language = Param.Choice("language", ContractLimits.LyricsLanguages);

    /// <inheritdoc />
    public IEnumerable<AgentToolDescriptor> GetTools() =>
    [
        Create("list_lyrics_sheets", ToolRiskDto.Read) with
        {
            Title = "List lyrics sheets",
            Description = "List the user's saved lyrics sheets (title and language only, never the text), optionally filtered on the title.",
            Schema = ToolSchema.Of(Param.Text("query", 1, 80).Optional()),
            Handler = ListSheetsAsync,
        },
        Create("save_lyrics_sheet", ToolRiskDto.Change) with
        {
            Title = "Save a lyrics sheet",
            Description = "Save lyrics the user typed in this conversation as a sheet (copy the text exactly). Lyrics from other sources are refused.",
            Schema = ToolSchema.Of(Param.Text("title", 1, 80), Language, Param.Text("text", 1, 4096)),
            UndoKind = UndoKindDto.Compensate,
            LyricsArguments = ["text"],
            Handler = SaveSheetAsync,
        },
        Create("generate_song", ToolRiskDto.Heavy) with
        {
            Title = "Generate a song",
            Description = "Make a new song from a saved sheet (lyricsId) or lyrics the user typed (lyricsText, copied exactly as typed), in a style (styleId) or a short caption, or instrumental. Title, style and voice are optional: do not ask for them first. GPU job: the user must confirm. Wait with wait_for_job phase generation.",
            Schema = SongSchema(),
            UndoKind = UndoKindDto.Compensate,
            LyricsArguments = ["lyricsText"],
            ImitationArguments = ["freeCaption", "delivery", "title"],
            Handler = GenerateAsync,
            Estimator = (call, _) => Task.FromResult(Estimate("Generate a song", call.Number("durationSeconds"), call.Whole("takes") ?? 1)),
        },
        Create("list_voice_profiles", ToolRiskDto.Read) with
        {
            Title = "List voice profiles",
            Description = "List the voice profiles made on the Voices page (display name, consent current, sample length).",
            Availability = ToolAvailability.VoiceService,
            Handler = ListProfilesAsync,
        },
        Create("convert_take_voice", ToolRiskDto.Heavy) with
        {
            Title = "Convert a take to an own voice",
            Description = "Sing the vocals of a generated song take with an existing voice profile (with current consent). GPU job: the user must confirm.",
            Schema = ToolSchema.Of(Param.Uuid("generationId"), Param.Whole("index", 1, 2), Param.Uuid("profileId"), Param.Whole("pitchShift", -12, 12).Optional(), Param.Flag("autoShift").Optional()),
            Availability = ToolAvailability.VoiceService,
            UndoKind = UndoKindDto.Compensate,
            TargetArgument = "generationId",
            Handler = ConvertAsync,
            Estimator = ConvertEstimateAsync,
            Precondition = async (call, cancellationToken) => await ConsentedProfileAsync(call, cancellationToken),
        },
    ];

    /// <summary>Undo of save_lyrics_sheet (undo-only tool <c>delete_lyrics_sheet</c>).</summary>
    internal static async Task<ToolResult> DeleteSheetAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        await call.Actions.Songs.DeleteLyricsSheetAsync(call.RequiredId("lyricsId"), cancellationToken);
        return ToolResult.Of(new JsonObject { ["deleted"] = true });
    }

    private static AgentToolDescriptor Create(string name, ToolRiskDto risk) =>
        Tool(F, name, ToolKindDto.Server, risk) with { Group = ToolGroup.Create, Availability = ToolAvailability.GenerationService };

    private static ToolSchema SongSchema() => ToolSchema.Of([.. SongTextParameters(), .. SongMusicParameters()])
        .AtMostOne("lyricsId", "lyricsText")
        .AtMostOne("styleId", "freeCaption");

    private static ToolParameter[] SongTextParameters() =>
    [
        Param.Uuid("lyricsId").Optional(),
        Param.Text("lyricsText", 1, 4096).Optional(),
        Param.Text("title", 1, 80).Optional(),
        Language,
        Param.Uuid("styleId").Optional(),
        Param.Text("freeCaption", 1, 300).Optional(),
        Param.Choice("leadVoice", "female", "male", "any"),
        Param.Text("delivery", 1, 40).Optional(),
    ];

    private static ToolParameter[] SongMusicParameters() =>
    [
        Param.Flag("instrumental").Optional(),
        Param.Whole("bpm", 40, 220).Optional(),
        Param.Text("keyScale", 1, 20).Optional(),
        Param.Whole("timeSignature", 2, 6).Optional().Describe("2, 3, 4 or 6."),
        Param.Number("durationSeconds", 10, 480).Optional(),
        Param.Whole("takes", 1, 2).Optional(),
    ];

    private static async Task<ToolResult> ListSheetsAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var query = Fold(call.Text("query"));
        var sheets = await call.Actions.Songs.ListLyricsSheetsAsync(cancellationToken);
        return ToolResult.Of(new JsonArray([.. sheets.Where(sheet => Fold(sheet.Title).Contains(query, StringComparison.Ordinal)).Take(MaxSheets).Select(sheet => (JsonNode)new JsonObject
        {
            ["lyricsId"] = sheet.LyricsId.ToString(),
            [ResultProjector.UntrustedField] = Untrusted(sheet.Title),
            ["language"] = TrustedText.Code(sheet.Language),
            ["updatedAt"] = sheet.UpdatedAt,
        })]));
    }

    private static async Task<ToolResult> SaveSheetAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var input = new LyricsInput { Title = call.Text("title")!, Language = call.Text("language")!, Text = call.Text("text")! };
        var sheet = await call.Actions.Songs.CreateLyricsSheetAsync(input, cancellationToken);
        return new ToolResult
        {
            Projection = new JsonObject { ["lyricsId"] = sheet.LyricsId.ToString(), ["title"] = input.Title, ["warnings"] = new JsonArray() },
            Undo = Undo("delete_lyrics_sheet", new JsonObject { ["lyricsId"] = sheet.LyricsId.ToString() }),
            UndoFresh = true,
        };
    }

    private static async Task<ToolResult> GenerateAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var request = SongRequestOf(call);
        var generation = await call.Actions.Songs.CreateSongAsync(request, cancellationToken);
        return Started(generation, JobPhaseDto.Generation, Estimate("Generate a song", request.DurationSeconds, request.Takes).EstimatedMinutes);
    }

    private static string? TimeSignature(int? time) => time is null or 2 or 3 or 4 or 6
        ? time?.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : throw new AgentActionException("Use a time signature of 2, 3, 4 or 6.");

    private static SongRequest SongRequestOf(ToolInvocation call) => MusicOf(call) with
    {
        LyricsId = call.Id("lyricsId"),
        LyricsText = call.Text("lyricsText"),
        Title = call.Text("title"),
        StyleId = call.Id("styleId"),
        FreeCaption = call.Text("freeCaption"),
        Delivery = call.Text("delivery"),
    };

    private static SongRequest MusicOf(ToolInvocation call) => new()
    {
        Language = call.Text("language")!,
        LeadVoice = Parse<LeadVoiceDto>(call.Text("leadVoice"))!.Value,
        Instrumental = call.Flag("instrumental") == true,
        Bpm = call.Whole("bpm"),
        KeyScale = call.Text("keyScale"),
        TimeSignature = TimeSignature(call.Whole("timeSignature")),
        DurationSeconds = call.Number("durationSeconds") ?? 120,
        Takes = call.Whole("takes") ?? 1,
    };

    private static async Task<ToolResult> ListProfilesAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var profiles = await call.Actions.Voices.ListVoiceProfilesAsync(cancellationToken);
        return ToolResult.Of(new JsonArray([.. profiles.Select(profile => (JsonNode)new JsonObject
        {
            ["profileId"] = profile.ProfileId.ToString(),
            [ResultProjector.UntrustedField] = Untrusted(profile.DisplayName),
            ["consentCurrent"] = profile.ConsentCurrent,
            ["durationSeconds"] = profile.DurationSeconds,
        })]));
    }

    /// <summary>Defense in depth (the backend's 409 stays authoritative): the profile exists and its consent is current.</summary>
    private static async Task<VoiceProfileSummary> ConsentedProfileAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var profileId = call.RequiredId("profileId");
        var profile = (await call.Actions.Voices.ListVoiceProfilesAsync(cancellationToken)).FirstOrDefault(item => item.ProfileId == profileId)
            ?? throw new AgentActionException(UnknownProfile);
        return profile.ConsentCurrent ? profile : throw new AgentActionException(ConsentNotCurrent);
    }

    private static async Task<ToolEstimate> ConvertEstimateAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        var profile = await ConsentedProfileAsync(call, cancellationToken);
        return Estimate($"Convert take {call.Whole("index")} to the voice \"{profile.DisplayName}\"", null, 1);
    }

    private static async Task<ToolResult> ConvertAsync(ToolInvocation call, CancellationToken cancellationToken)
    {
        await ConsentedProfileAsync(call, cancellationToken);
        var request = new VoiceConversionRequest { ProfileId = call.RequiredId("profileId"), PitchShift = call.Whole("pitchShift") ?? 0, AutoShift = call.Flag("autoShift") ?? true };
        var generation = await call.Actions.Voices.CreateVoiceConversionAsync(call.RequiredId("generationId"), call.Whole("index")!.Value, request, cancellationToken);
        return Started(generation, JobPhaseDto.Generation, Estimate("Voice conversion", null, 1).EstimatedMinutes);
    }
}
