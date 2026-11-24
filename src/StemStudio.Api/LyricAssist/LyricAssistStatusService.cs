using StemStudio.Api.Agent;
using StemStudio.Api.Tracks;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Status and global settings of the assistant (FR-025 - FR-027): the model layer mirrors the 011 agent status (3 s cap of the probe)
/// combined with the ai-agent factory; the lyric-assistant Claude notice is versioned and stored locally.
/// </summary>
/// <param name="stores">Stores.</param>
/// <param name="agentStatus">011 status probe.</param>
/// <param name="modelLayer">Model layer (factory).</param>
/// <param name="clock">Clock.</param>
public sealed class LyricAssistStatusService(LyricAssistStores stores, AgentStatusProbe agentStatus, LyricModelLayer modelLayer, TimeProvider clock)
{
    /// <summary>The notice text of <see cref="LyricAssistLimits.ClaudeNoticeVersion"/>.</summary>
    public const string ClaudeNoticeText =
        "With Claude selected, the lyric assistant sends the text of the lyrics you check to Anthropic (Claude), a paid online service. " +
        "Each check, suggestion or title idea is billed to your Claude key. With Local AI (Ollama) nothing leaves this computer.";

    /// <summary>
    /// The standing quality note with the local model (SC-001, constitution V): the 2026-11-24 reference-machine run of the lyric
    /// suite (023 T068, qwen3.5:9b) found almost no grammar or flow errors in either language. Shown until a later run meets SC-001.
    /// </summary>
    public const string LocalModelQualityNote =
        "Grammar and flow checks are limited with the local model: it finds few grammar or flow mistakes in Dutch and English. " +
        "Spelling still uses the dictionaries; rhymes, alternatives and title ideas are not affected.";

    /// <summary>The status.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200.</returns>
    public async Task<IResult> StatusAsync(CancellationToken cancellationToken)
    {
        var agent = await agentStatus.GetAsync(cancellationToken);
        var model = await modelLayer.ResolveAsync(string.Empty, AssistKind.Check, cancellationToken);
        var settings = await stores.Words.SettingsAsync(cancellationToken);
        var active = await stores.Assists.ActiveAsync(cancellationToken);
        return TypedResults.Ok(new LyricAssistStatusDto
        {
            Languages = [AssistLanguageDto.Nl, AssistLanguageDto.En],
            Dictionary = new LayerInfoDto { Layer = SuggestionSourceDto.Dictionary, Name = "OpenTaal (nl), SCOWL + CMUdict (en)", Available = true },
            Model = Model(agent, model),
            ClaudeNoticeRequired = model.Backend == AgentBackendDto.Claude && !settings.HasAcknowledged(LyricAssistLimits.ClaudeNoticeVersion),
            AssistActive = active is [var first, ..] ? first.Id : null,
        });
    }

    /// <summary>The settings.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200.</returns>
    public async Task<IResult> SettingsAsync(CancellationToken cancellationToken) =>
        TypedResults.Ok(ToDto(await stores.Words.SettingsAsync(cancellationToken)));

    /// <summary>Acknowledges or withdraws the Claude notice.</summary>
    /// <param name="update">The update.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 400.</returns>
    public async Task<IResult> UpdateSettingsAsync(LyricAssistSettingsUpdate? update, CancellationToken cancellationToken)
    {
        if (update is null)
        {
            return ApiProblems.Validation("acknowledgeClaudeNotice", "Send true or false.");
        }

        var settings = await stores.Words.SettingsAsync(cancellationToken);
        settings.SetClaudeNotice(update.AcknowledgeClaudeNotice, LyricAssistLimits.ClaudeNoticeVersion, clock.GetUtcNow().UtcDateTime);
        await stores.Words.SaveAsync(cancellationToken);
        return TypedResults.Ok(ToDto(settings));
    }

    // The fake backend's lyric model comes from STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT (DELTA.md section 4), not from the 011 agent
    // script: only the lyric factory decides whether it is ready.
    private static LyricAssistModelStatusDto Model(AgentStatusDto agent, ModelLayerState model) =>
        agent.Backend == AgentBackendDto.Fake ? FakeModel(agent, model) : MirroredModel(agent, model);

    private static LyricAssistModelStatusDto FakeModel(AgentStatusDto agent, ModelLayerState model) => new()
    {
        Backend = agent.Backend,
        Ready = model.Available,
        ActiveModel = model.Available ? agent.ActiveModel : null,
        UsingFallback = false,
        Reason = model.Available ? null : model.Reason,
        Fix = model.Available ? null : model.Fix,
    };

    private static LyricAssistModelStatusDto MirroredModel(AgentStatusDto agent, ModelLayerState model) => new()
    {
        Backend = agent.Backend,
        Ready = agent.Ready && model.Available,
        ActiveModel = agent.ActiveModel,
        UsingFallback = agent.UsingFallback,
        Reason = model.Available ? agent.Reason : model.Reason,
        Fix = model.Available ? agent.Fix : model.Fix,
        QualityNote = agent.Backend == AgentBackendDto.Ollama ? LocalModelQualityNote : null,
    };

    private static LyricAssistSettingsDto ToDto(LyricAssistSettings settings)
    {
        var acknowledged = settings.HasAcknowledged(LyricAssistLimits.ClaudeNoticeVersion);
        return new LyricAssistSettingsDto
        {
            ClaudeNoticeAcknowledged = acknowledged,
            ClaudeNoticeAcknowledgedAt = acknowledged ? TrackMapper.Utc(settings.ClaudeNoticeAcknowledgedAt!.Value) : null,
            ClaudeNoticeVersion = LyricAssistLimits.ClaudeNoticeVersion,
            ClaudeNoticeText = ClaudeNoticeText,
            OwnWorkStatementVersion = OwnWorkOverride.CurrentStatementVersion,
            OwnWorkStatementText = OwnWorkOverride.StatementText,
        };
    }
}
