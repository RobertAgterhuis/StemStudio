using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.Persistence.Agent;
using static StemStudio.Api.Agent.Stores.AgentStoreValues;

namespace StemStudio.Api.Agent.Stores;

/// <summary>
/// EF Core implementation of <see cref="IAgentSettingsStore"/> (feature 011, T021): the single row, or the defaults with the
/// backend from <c>StemStudio__Agent__Backend</c> while none is saved. <c>StemStudio__Agent__Backend=fake</c> (the E2E test
/// stack) overrides the stored backend for the agent runtime, so a persisted row can never switch the fake stack to a real
/// model; the settings endpoints read the stored choice through <see cref="GetStoredAsync"/>.
/// </summary>
/// <param name="db">Context per operation.</param>
/// <param name="options">Host options.</param>
/// <param name="clock">Clock for the defaults' time.</param>
public sealed class EfAgentSettingsStore(AgentDb db, IOptions<AgentHostOptions> options, TimeProvider clock) : IAgentSettingsStore
{
    /// <inheritdoc />
    public async Task<AgentSettings> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await GetStoredAsync(cancellationToken);
        return options.Value.InitialBackend() == AgentBackendDto.Fake ? settings with { Backend = AgentBackendDto.Fake } : settings;
    }

    /// <summary>The stored settings (or the defaults), without the fake-backend override of the environment.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The settings as saved.</returns>
    public Task<AgentSettings> GetStoredAsync(CancellationToken cancellationToken) => db.WithAsync(async context =>
    {
        var entity = await context.AgentSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return entity is null ? AgentSettings.Defaults(options.Value.InitialBackend(), clock.GetUtcNow()) : ToRecord(entity);
    });

    /// <inheritdoc />
    public Task SaveAsync(AgentSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return db.SaveAsync(async context =>
        {
            var entity = await context.AgentSettings.SingleOrDefaultAsync(cancellationToken);
            Apply(entity ?? context.AgentSettings.Add(new AgentSettingsEntity()).Entity, settings);
        }, cancellationToken);
    }

    private static void Apply(AgentSettingsEntity entity, AgentSettings settings)
    {
        entity.Backend = settings.Backend.ToString();
        entity.OllamaModel = settings.OllamaModel;
        entity.OllamaFallbackModel = settings.OllamaFallbackModel;
        entity.OllamaThink = settings.OllamaThink;
        entity.ClaudeModel = settings.ClaudeModel.ToString();
        entity.ClaudeEffort = settings.ClaudeEffort.ToString();
        entity.ClaudeNoticeAcknowledgedAt = Utc(settings.ClaudeNoticeAcknowledgedAt);
        entity.Limits = ToJson(settings.Limits);
        entity.UpdatedAt = Utc(settings.UpdatedAt);
    }

    private static AgentSettings ToRecord(AgentSettingsEntity entity) => new()
    {
        Backend = Parse<AgentBackendDto>(entity.Backend),
        OllamaModel = entity.OllamaModel,
        OllamaFallbackModel = entity.OllamaFallbackModel,
        OllamaThink = entity.OllamaThink,
        ClaudeModel = Parse<ClaudeModelDto>(entity.ClaudeModel),
        ClaudeEffort = Parse<ClaudeEffortDto>(entity.ClaudeEffort),
        ClaudeNoticeAcknowledgedAt = Offset(entity.ClaudeNoticeAcknowledgedAt),
        Limits = FromJson<AgentLimitsDto>(entity.Limits),
        UpdatedAt = Offset(entity.UpdatedAt),
    };
}
