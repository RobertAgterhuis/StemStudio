using System.Net;
using NSubstitute;
using StemStudio.Api.Agent.Secrets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// 011 follow-up claude-key T038 [sec]: <c>POST /api/agent/settings/test</c> with <c>claudeKeySource</c> (CK-FR-007, CK-FR-012).
/// The Claude case runs the agent library's real probe against a local stand-in for Anthropic; Anthropic is never contacted.
/// </summary>
public sealed class ClaudeKeyBackendTestTests : IDisposable
{
    private const string ModelJson = """{"type":"model","id":"claude-opus-5-5","display_name":"Claude Opus 5.5","created_at":"2026-10-01T00:00:00Z"}""";
    private readonly ClaudeKeyApi _api = new(new() { RealStatusService = true });

    public ClaudeKeyBackendTestTests()
    {
        _api.AnthropicReply = _ => StubHttpMessageHandler.Json(HttpStatusCode.OK, ModelJson);
        _api.UseEnvironmentKey(ClaudeKeySentinels.Environment);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _api.Dispose();

    [Theory]
    [InlineData(null, "environment")]
    [InlineData("active", "environment")]
    [InlineData("environment", "environment")]
    [InlineData("stored", "stored")]
    public async Task ClaudeTest_UsesTheKeyOfTheRequestedSource(string? source, string expected)
    {
        using var stored = await _api.PutKeyAsync(ClaudeKeySentinels.Stored);

        using var response = await TestAsync(source is null ? """{"backend":"claude"}""" : $$"""{"backend":"claude","claudeKeySource":"{{source}}"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "testAgentBackend");
        var result = await response.BodyAsync<BackendTestResultDto>();
        (result.Ok, result.KeySource).ShouldBe((true, expected == "stored" ? ClaudeKeySourceDto.Stored : ClaudeKeySourceDto.Environment));
        _api.AnthropicKeys.ShouldBe([expected == "stored" ? ClaudeKeySentinels.Stored : ClaudeKeySentinels.Environment]);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain(ClaudeKeySentinels.Stored);
        body.ShouldNotContain(ClaudeKeySentinels.Environment);
    }

    [Fact]
    public async Task ClaudeTestOfAMissingStoredKey_IsNotOkWithoutARequest()
    {
        using var response = await TestAsync("""{"backend":"claude","claudeKeySource":"stored"}""");

        var result = await response.BodyAsync<BackendTestResultDto>();
        (result.Ok, result.KeySource).ShouldBe((false, (ClaudeKeySourceDto?)null));
        _api.Anthropic.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("192.168.1.20", null, null)]
    [InlineData("127.0.0.1", "stemstudio.lan", null)]
    [InlineData("127.0.0.1", null, "http://evil.test")]
    public async Task ClaudeTestFromAnotherDevice_Is403LocalOnlyWithoutARequest(string peer, string? host, string? origin)
    {
        using var response = await TestAsync("""{"backend":"claude"}""", peer, host, origin);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ContractAssert.MatchesAsync(response, "testAgentBackend");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(AgentProblemTypes.Uri(AgentProblemTypes.LocalOnly));
        _api.Anthropic.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task OllamaTestFromAnotherDevice_IsUnchanged()
    {
        using var api = new ClaudeKeyApi();
        var expected = new BackendTestResultDto { Backend = AgentBackendDto.Ollama, Ok = true, LatencyMs = 3, Detail = "Ollama answered" };
        api.StatusService.TestAsync(AgentBackendDto.Ollama, ClaudeKeyTestSourceDto.Stored, Arg.Any<CancellationToken>()).Returns(expected);

        using var response = await api.SendAsync(Test("""{"backend":"ollama","claudeKeySource":"stored"}""") with { Peer = "192.168.1.20" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.BodyAsync<BackendTestResultDto>()).ShouldBe(expected);
    }

    [Theory]
    [InlineData("""{"backend":"claude"}""", ClaudeKeyTestSourceDto.Active)]
    [InlineData("""{"backend":"claude","claudeKeySource":"stored"}""", ClaudeKeyTestSourceDto.Stored)]
    [InlineData("""{"backend":"claude","claudeKeySource":"environment"}""", ClaudeKeyTestSourceDto.Environment)]
    public async Task ClaudeTestThroughAnyStatusService_PassesTheRequestedKeySource(string body, ClaudeKeyTestSourceDto source)
    {
        using var api = new ClaudeKeyApi();
        var expected = new BackendTestResultDto { Backend = AgentBackendDto.Claude, Ok = true, LatencyMs = 1, Detail = "ok", KeySource = ClaudeKeySourceDto.Stored };
        api.StatusService.TestAsync(AgentBackendDto.Claude, source, Arg.Any<CancellationToken>()).Returns(expected);

        using var response = await api.SendAsync(Test(body));

        (await response.BodyAsync<BackendTestResultDto>()).ShouldBe(expected);
        await api.StatusService.DidNotReceive().TestAsync(Arg.Any<AgentBackendDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownKeySource_Is400()
    {
        using var response = await TestAsync("""{"backend":"claude","claudeKeySource":"both"}""");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        _api.Anthropic.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefusedTest_IsLoggedWithTheFilterRateLimit()
    {
        using (await TestAsync("""{"backend":"claude"}""", "192.168.1.20"))
        {
        }

        _api.Factory.Logs.For(nameof(LocalOnlyKeyEntryFilter)).Count().ShouldBe(1);
    }

    private Task<HttpResponseMessage> TestAsync(string body, string peer = "127.0.0.1", string? host = null, string? origin = null) =>
        _api.SendAsync(Test(body) with { Peer = peer, Host = host, Origin = origin });

    private static KeyRequest Test(string body) => new(HttpMethod.Post, ApiRoutes.AgentSettingsTest) { Body = body };
}
