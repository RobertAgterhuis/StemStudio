using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Agent.Backends;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// 011 follow-up claude-key T023: <c>GET</c>/<c>PUT /api/agent/settings/claude-key</c> [sec] (CK-FR-006, CK-FR-008..010,
/// CK-FR-015). The key store and the key provider are real; Anthropic is a handler that must never be called.
/// </summary>
public sealed class ClaudeKeyEndpointsTests : IDisposable
{
    private readonly ClaudeKeyApi _api = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Get_WithNothingConfigured_IsNotConfigured()
    {
        using var response = await _api.GetStatusAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getClaudeKeyStatus");
        (await response.BodyAsync<ClaudeKeyStatusDto>()).ShouldBe(ClaudeKeyStatusDto.NotConfigured);
    }

    [Fact]
    public async Task Get_WithTheEnvironmentKey_ReportsItsSourceAndLastFour()
    {
        _api.UseEnvironmentKey(ClaudeKeySentinels.Environment);

        var status = await _api.StatusAsync();

        (status.Configured, status.Source, status.State, status.LastFour, status.Stored)
            .ShouldBe((true, ClaudeKeySourceDto.Environment, ClaudeKeyStateDto.Environment, "ENV1", StoredClaudeKeyStateDto.Absent));
    }

    [Fact]
    public async Task Put_StoresTheKeyAndReturnsTheStatusWithoutTheValue()
    {
        using var response = await _api.PutKeyAsync("  " + ClaudeKeySentinels.Stored + "\n");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "storeClaudeKey");
        await ShouldNotLeakAsync(response, ClaudeKeySentinels.Stored);
        var status = await response.BodyAsync<ClaudeKeyStatusDto>();
        (status.Configured, status.Source, status.State, status.LastFour).ShouldBe((true, ClaudeKeySourceDto.Stored, ClaudeKeyStateDto.Stored, "STO1"));
        (status.Stored, status.StoredLastFour, status.StoredAt, status.StoreAvailable).ShouldBe((StoredClaudeKeyStateDto.Readable, "STO1", ApiFactory.Start, true));
        (await _api.StatusAsync()).ShouldBe(status);
    }

    [Fact]
    public async Task Put_WhileTheEnvironmentKeyIsSet_KeepsTheEnvironmentActive()
    {
        _api.UseEnvironmentKey(ClaudeKeySentinels.Environment);

        using var response = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);

        var status = await response.BodyAsync<ClaudeKeyStatusDto>();
        (status.State, status.Source, status.LastFour, status.Stored, status.StoredLastFour)
            .ShouldBe((ClaudeKeyStateDto.Environment, ClaudeKeySourceDto.Environment, "ENV1", StoredClaudeKeyStateDto.Readable, "STO1"));
    }

    [Fact]
    public async Task Get_WithAKeyFromAnotherKeyRing_IsStoredUnreadableWithAFix()
    {
        var dataDir = Directory.CreateTempSubdirectory("stemstudio-ck-").FullName;
        try
        {
            using (var first = new ClaudeKeyApi(dataDir))
            {
                using var stored = await first.PutKeyAsync(ClaudeKeySentinels.Stored);
                stored.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            using var other = new ClaudeKeyApi(dataDir, keyRingDir: dataDir + "-other-keys");
            using var response = await other.GetStatusAsync();

            await ContractAssert.MatchesAsync(response, "getClaudeKeyStatus");
            var status = await response.BodyAsync<ClaudeKeyStatusDto>();
            (status.Configured, status.State, status.Stored, status.StoredLastFour, status.LastFour)
                .ShouldBe((false, ClaudeKeyStateDto.StoredUnreadable, StoredClaudeKeyStateDto.Unreadable, "STO1", (string?)null));
            (status.Reason, status.Fix).ShouldBe((ClaudeKeyProvider.UnreadableReason, ClaudeKeyProvider.UnreadableFix));
        }
        finally
        {
            Directory.Delete(dataDir + "-other-keys", true);
            Directory.Delete(dataDir, true);
        }
    }

    public static TheoryData<string, string> InvalidKeys => new()
    {
        { "   ", ClaudeKeyInvalidReasons.Empty },
        { "sk-" + "openai-" + new string('a', 40), ClaudeKeyInvalidReasons.Prefix },
        { ClaudeKeySentinels.Prefix + "api03-short", ClaudeKeyInvalidReasons.Length },
        { ClaudeKeySentinels.Prefix + "api03-" + new string('a', 300), ClaudeKeyInvalidReasons.Length },
        { ClaudeKeySentinels.Prefix + "api03-with space-" + new string('a', 30), ClaudeKeyInvalidReasons.Characters },
        { ClaudeKeySentinels.Admin, ClaudeKeyInvalidReasons.AdminKey },
    };

    [Theory]
    [MemberData(nameof(InvalidKeys))]
    public async Task Put_InvalidKey_Is422WithTheReasonAndWithoutTheValue(string value, string reason)
    {
        using var response = await _api.PutKeyAsync(value);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "storeClaudeKey");
        var problem = await response.JsonAsync();
        problem.GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.ClaudeKeyInvalid));
        problem.GetProperty("reason").GetString().ShouldBe(reason);
        if (value.Trim().Length > 0)
        {
            await ShouldNotLeakAsync(response, value.Trim());
        }

        (await _api.StatusAsync()).Stored.ShouldBe(StoredClaudeKeyStateDto.Absent);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"apiKey":42}""")]
    [InlineData("""{"apiKey":null}""")]
    [InlineData("""["apiKey"]""")]
    [InlineData("")]
    public async Task Put_BodyWithoutAnApiKeyString_Is400WithAFixedText(string body)
    {
        using var response = await _api.PutRawAsync(body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(response, "storeClaudeKey");
        var problem = await response.JsonAsync();
        problem.GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.InvalidBody));
        problem.GetProperty("detail").GetString().ShouldBe(ClaudeKeyEndpoints.InvalidBodyDetail);
    }

    [Fact]
    public async Task Put_BadJsonContainingTheKey_NeverQuotesIt()
    {
        using var response = await _api.PutRawAsync("{\"apiKey\": \"" + ClaudeKeySentinels.Stored);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldNotLeakAsync(response, ClaudeKeySentinels.Stored);
    }

    [Fact]
    public async Task Put_KeyWithPropertyNameInOtherCase_IsAccepted()
    {
        using var response = await _api.PutRawAsync("{\"ApiKey\":\"" + ClaudeKeySentinels.Stored + "\"}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Put_BodyOver1KiB_Is413()
    {
        using var response = await _api.PutKeyAsync(ClaudeKeySentinels.Stored + new string('a', AgentContractLimits.ClaudeKeyBodyBytes));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        await ContractAssert.MatchesAsync(response, "storeClaudeKey");
        await ShouldNotLeakAsync(response, ClaudeKeySentinels.Stored);
        (await _api.StatusAsync()).Stored.ShouldBe(StoredClaudeKeyStateDto.Absent);
    }

    [Fact]
    public async Task Put_ChunkedBodyOver1KiB_Is413()
    {
        var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(new string(' ', 2000) + "{}")));
        content.Headers.ContentType = new("application/json");
        using var request = new HttpRequestMessage(HttpMethod.Put, ApiRoutes.AgentClaudeKey) { Content = content };
        request.Headers.TransferEncodingChunked = true;

        using var response = await _api.Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Put_BodyOfExactly1KiB_IsRead()
    {
        var json = "{\"apiKey\":\"" + ClaudeKeySentinels.Stored + "\"}";
        using var response = await _api.PutRawAsync(json + new string(' ', AgentContractLimits.ClaudeKeyBodyBytes - json.Length));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Put_WithAnUnavailableKeyStore_Is503AfterTheFormatCheck()
    {
        var dataDir = Directory.CreateTempSubdirectory("stemstudio-ck-").FullName;
        try
        {
            using var api = new ClaudeKeyApi(dataDir, keyRingDir: Path.Combine(dataDir, "keys"));

            using var invalid = await api.PutKeyAsync("nope");
            using var response = await api.PutKeyAsync(ClaudeKeySentinels.Stored);

            invalid.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            await ContractAssert.MatchesAsync(response, "storeClaudeKey");
            var problem = await response.JsonAsync();
            problem.GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.KeyStoreUnavailable));
            problem.GetProperty("detail").GetString().ShouldBe(ClaudeKeyProvider.StoreUnavailableReason("inside-library"));
            var status = await api.StatusAsync();
            (status.StoreAvailable, status.Stored).ShouldBe((false, StoredClaudeKeyStateDto.Absent));
        }
        finally
        {
            Directory.Delete(dataDir, true);
        }
    }

    [Fact]
    public async Task Put_NeverContactsAnthropicNorChangesTheBackendOrTheNotice()
    {
        var before = await _api.SettingsAsync();

        using var stored = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);
        using var replaced = await _api.PutKeyAsync(ClaudeKeySentinels.Replacement);
        using var status = await _api.GetStatusAsync();

        replaced.StatusCode.ShouldBe(HttpStatusCode.OK);
        _api.Anthropic.Requests.ShouldBeEmpty();
        var after = await _api.SettingsAsync();
        (after.Backend, after.ClaudeNoticeAcknowledged).ShouldBe((before.Backend, before.ClaudeNoticeAcknowledged));
        (await status.BodyAsync<ClaudeKeyStatusDto>()).LastFour.ShouldBe("STO2");
    }

    [Fact]
    public async Task ClaudeSelection_AcceptsAStoredKey()
    {
        using var refused = await _api.Client.PutAsync(ApiRoutes.AgentSettings, Json("""{"backend":"claude","acknowledgeClaudeNotice":true}"""), Ct);
        using var stored = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);
        using var accepted = await _api.Client.PutAsync(ApiRoutes.AgentSettings, Json("""{"backend":"claude","acknowledgeClaudeNotice":true}"""), Ct);

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await accepted.BodyAsync<AgentSettingsDto>()).Backend.ShouldBe(AgentBackendDto.Claude);
        _api.Anthropic.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AgentStatusFallback_CarriesTheKeyStatus()
    {
        _api.StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("probe failed"));
        using var stored = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);

        using var response = await _api.Client.GetAsync(ApiRoutes.AgentStatus, Ct);

        await ContractAssert.MatchesAsync(response, "getAgentStatus");
        var claude = (await response.BodyAsync<AgentStatusDto>()).Claude;
        (claude.KeyConfigured, claude.Key.State, claude.Key.LastFour).ShouldBe((true, ClaudeKeyStateDto.Stored, "STO1"));
        await ShouldNotLeakAsync(response, ClaudeKeySentinels.Stored);
    }

    [Fact]
    public void KeyRoute_DisablesHttpLoggingInItsMetadata()
    {
        var endpoints = _api.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.TrimEnd('/') == ApiRoutes.AgentClaudeKey).ToList();

        endpoints.Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName).ShouldContain("storeClaudeKey");
        endpoints.ShouldAllBe(endpoint => endpoint.Metadata.GetMetadata<HttpLoggingAttribute>()!.LoggingFields == HttpLoggingFields.None);
    }

    [Fact]
    public async Task HostThatLogsEveryRequest_LogsNothingOfTheKeyRoute()
    {
        using var api = new ClaudeKeyApi(httpLogging: true);
        using var health = await api.Client.GetAsync(ApiRoutes.Health, Ct);
        using var stored = await api.PutKeyAsync(ClaudeKeySentinels.Stored);
        using var invalid = await api.PutRawAsync("{\"apiKey\": \"" + ClaudeKeySentinels.Replacement);
        using var status = await api.GetStatusAsync();

        stored.StatusCode.ShouldBe(HttpStatusCode.OK);
        var httpLogs = api.Factory.Logs.For("HttpLogging.HttpLoggingMiddleware").Select(entry => entry.Message).ToList();
        httpLogs.ShouldContain(message => message.Contains(ApiRoutes.Health, StringComparison.Ordinal));
        httpLogs.ShouldAllBe(message => !message.Contains("claude-key", StringComparison.Ordinal));
        ShouldNotLeak(api.Factory.Logs, ClaudeKeySentinels.Stored);
        ShouldNotLeak(api.Factory.Logs, ClaudeKeySentinels.Replacement);
    }

    [Fact]
    public void Interceptor_LeavesOtherRoutesAlone()
    {
        KeyEndpointLoggingInterceptor.IsKeyRoute(new PathString(ApiRoutes.Health)).ShouldBeFalse();
        KeyEndpointLoggingInterceptor.IsKeyRoute(new PathString(ApiRoutes.AgentSettings)).ShouldBeFalse();
        KeyEndpointLoggingInterceptor.IsKeyRoute(new PathString(ApiRoutes.AgentClaudeKey.ToUpperInvariant())).ShouldBeTrue();
        KeyEndpointLoggingInterceptor.IsKeyRoute(new PathString(ApiRoutes.AgentClaudeKey + "/")).ShouldBeTrue();
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task ShouldNotLeakAsync(HttpResponseMessage response, string sentinel)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        var headers = response.Headers.ToString() + response.Content.Headers;
        foreach (var form in ClaudeKeySentinels.SearchForms(sentinel))
        {
            body.ShouldNotContain(form);
            headers.ShouldNotContain(form);
        }
    }

    private static void ShouldNotLeak(ListLoggerProvider logs, string sentinel)
    {
        foreach (var form in ClaudeKeySentinels.SearchForms(sentinel))
        {
            logs.Entries.ShouldAllBe(entry => !entry.Message.Contains(form, StringComparison.Ordinal) && (entry.Exception == null || !entry.Exception.ToString().Contains(form, StringComparison.Ordinal)));
        }
    }
}
