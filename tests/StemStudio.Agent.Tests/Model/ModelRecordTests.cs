using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Model;

/// <summary>Skeleton tests (architect, 011 Phase 0 T004): invariants of the agent records (data-model 2 and 4).</summary>
public sealed class ModelRecordTests
{
    [Fact]
    public void TokenUsage_AddSumsEveryCounter()
    {
        var sum = new TokenUsage(10, 2, 4).Add(new TokenUsage(5, 1, 0));

        sum.ShouldBe(new TokenUsage(15, 3, 4));
        Should.Throw<ArgumentNullException>(() => TokenUsage.Zero.Add(null!));
    }

    [Fact]
    public void Approval_ExpiresOnlyWhilePendingAfter24Hours()
    {
        var pending = Samples.Approval(ApprovalStatusDto.Pending, Samples.Now);

        pending.IsExpiredAt(Samples.Now.AddHours(23).AddMinutes(59)).ShouldBeFalse();
        pending.IsExpiredAt(Samples.Now.AddHours(24)).ShouldBeTrue();
        (pending with { Status = ApprovalStatusDto.Approved }).IsExpiredAt(Samples.Now.AddDays(3)).ShouldBeFalse();
        Approval.Lifetime.ShouldBe(TimeSpan.FromHours(24));
    }

    [Fact]
    public void Settings_DefaultsFollowDataModel2()
    {
        var settings = AgentSettings.Defaults(AgentBackendDto.Ollama, Samples.Now);

        settings.Backend.ShouldBe(AgentBackendDto.Ollama);
        settings.OllamaModel.ShouldBe("qwen3.5:9b");
        settings.OllamaFallbackModel.ShouldBe("qwen3.5:4b");
        settings.OllamaThink.ShouldBeFalse();
        settings.ClaudeModel.ShouldBe(ClaudeModelDto.ClaudeOpus55);
        settings.ClaudeEffort.ShouldBe(ClaudeEffortDto.Low);
        settings.ClaudeNoticeAcknowledgedAt.ShouldBeNull();
        settings.Limits.ShouldBe(AgentLimitsDto.Default);
        settings.UpdatedAt.ShouldBe(Samples.Now);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void Settings_AllowClaudeOnlyWithKeyAndAcknowledgedNotice(bool keyConfigured, bool acknowledged, bool allowed)
    {
        var settings = AgentSettings.Defaults(AgentBackendDto.Ollama, Samples.Now) with
        {
            ClaudeNoticeAcknowledgedAt = acknowledged ? Samples.Now : null,
        };

        settings.AllowsClaude(keyConfigured).ShouldBe(allowed);
    }

    [Fact]
    public void Limits_DefaultsFollowDataModel2()
    {
        var limits = AgentLimitsDto.Default;

        (limits.MaxModelTurns, limits.MaxToolCalls, limits.MaxInvalidCalls).ShouldBe((12, 20, 3));
        (limits.TurnTimeoutSeconds, limits.ClaudeTurnTimeoutSeconds).ShouldBe((120, 180));
        (limits.ClaudeMaxInputTokensPerRun, limits.ClaudeMaxOutputTokensPerRun, limits.MaxJobWaitMinutes).ShouldBe((60_000, 8_000, 45));
    }
}
