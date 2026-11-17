using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.Ports;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>023 T061: status mirrors 011, the Claude notice is versioned and required before any Claude call, no outbound requests (FR-025 - FR-027, SC-006).</summary>
public sealed class LyricAssistStatusTests : AssistTestBase
{
    [Fact]
    public async Task Status_ListsLayersAndTheActiveAssist()
    {
        using var status = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistStatus, UriKind.Relative), Ct);

        var body = await status.BodyAsync<LyricAssistStatusDto>();
        (body.Languages.Count, body.Dictionary.Available, body.ClaudeNoticeRequired, body.AssistActive).ShouldBe((2, true, false, null));
        await ContractAssert.MatchesAsync(status, "getLyricAssistStatus");
    }

    [Fact]
    public async Task Status_WithoutModel_ReportsTheReasonAndFix()
    {
        Assistants.Assistant = null;

        using var status = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistStatus, UriKind.Relative), Ct);

        (await status.BodyAsync<LyricAssistStatusDto>()).Model.ShouldSatisfyAllConditions(
            m => m.Ready.ShouldBeFalse(), m => m.Reason.ShouldBe("model-unavailable"), m => m.Fix.ShouldBe(Assistants.Fix));
    }

    [Fact]
    public async Task ClaudeBackend_NeedsTheCurrentNoticeBeforeAnyModelCall()
    {
        await SelectClaudeAsync();
        var sheet = await SheetAsync("the cat sat");

        using var refused = await CreateAsync(sheet.Id, Check());
        using var required = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistStatus, UriKind.Relative), Ct);
        using var ack = await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricAssistSettings, UriKind.Relative), new LyricAssistSettingsUpdate { AcknowledgeClaudeNotice = true }, Ct);
        var assist = await RunToEndAsync(sheet.Id, Check());
        using var withdraw = await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricAssistSettings, UriKind.Relative), new LyricAssistSettingsUpdate { AcknowledgeClaudeNotice = false }, Ct);
        using var again = await CreateAsync(sheet.Id, Check());

        ((await refused.JsonAsync()).GetProperty("code").GetString(), (await required.BodyAsync<LyricAssistStatusDto>()).ClaudeNoticeRequired).ShouldBe(("claude-notice-required", true));
        ((ScriptedAssistant)Assistants.Assistant!).Jobs.Count.ShouldBe(1);
        (await ack.BodyAsync<LyricAssistSettingsDto>()).ShouldSatisfyAllConditions(s => s.ClaudeNoticeAcknowledged.ShouldBeTrue(), s => s.ClaudeNoticeAcknowledgedAt.ShouldNotBeNull());
        assist.Status.ShouldBe(AssistStatusDto.Completed);
        (await withdraw.BodyAsync<LyricAssistSettingsDto>()).ClaudeNoticeAcknowledged.ShouldBeFalse();
        (await again.JsonAsync()).GetProperty("code").GetString().ShouldBe("claude-notice-required");
        await ContractAssert.MatchesAsync(refused, "createLyricAssist");
        await ContractAssert.MatchesAsync(ack, "updateLyricAssistSettings");
    }

    [Fact]
    public async Task Settings_GetAndInvalidUpdate()
    {
        using var get = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistSettings, UriKind.Relative), Ct);
        using var invalid = await Client.PutAsync(new Uri(ApiRoutes.LyricAssistSettings, UriKind.Relative), JsonContent.Create<object?>(null), Ct);

        (await get.BodyAsync<LyricAssistSettingsDto>()).ShouldSatisfyAllConditions(
            s => s.ClaudeNoticeVersion.ShouldBe("lyric-claude-notice.v1"), s => s.OwnWorkStatementVersion.ShouldBe("own-work.v1"),
            s => s.ClaudeNoticeAcknowledged.ShouldBeFalse(), s => s.OwnWorkStatementText.ShouldStartWith("This text is my own work"));
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(get, "getLyricAssistSettings");
        await ContractAssert.MatchesAsync(invalid, "updateLyricAssistSettings");
    }

    [Fact]
    public async Task AssistantActions_SendNoRequestOffTheMachine()
    {
        var sheet = await SheetAsync("the cat sat\non the mat nigth");

        var assist = await RunToEndAsync(sheet.Id, Check());
        (await Client.GetAsync(new Uri(ApiRoutes.LyricsSuggestions(sheet.Id), UriKind.Relative), Ct)).Dispose();
        (await Client.GetAsync(new Uri($"{ApiRoutes.LyricAssistRhymes}?word=night&language=en", UriKind.Relative), Ct)).Dispose();
        (await Client.GetAsync(new Uri(ApiRoutes.LyricsSyllables(sheet.Id), UriKind.Relative), Ct)).Dispose();

        assist.Status.ShouldBe(AssistStatusDto.Completed);
        Outbound.ShouldBeEmpty();
    }

    private Task<bool> SelectClaudeAsync() => Factory.WithScopeAsync(async sp =>
    {
        var store = sp.GetRequiredService<IAgentSettingsStore>();
        await store.SaveAsync((await store.GetAsync(Ct)) with { Backend = AgentBackendDto.Claude }, Ct);
        return true;
    });
}
