using System.Net;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Agent;

/// <summary>The Claude key calls of backend API 1.21.0 (<c>/api/agent/settings/claude-key</c>, 011 follow-up claude-key).</summary>
public sealed class ClaudeKeyApiClientTests : IDisposable
{
    private const string Route = "/api/agent/settings/claude-key";
    private readonly SseHttpHandler _handler = new();
    private readonly AgentApiClient _client;

    public ClaudeKeyApiClientTests() => _client = new AgentApiClient(SseHttpHandler.CreateClient(_handler));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _handler.Dispose();

    [Fact]
    public async Task GetStatus_ReadsTheKeyStatus()
    {
        _handler.Json(HttpStatusCode.OK, AgentData.Serialize(ClaudeKeyData.Stored()));

        var status = await _client.GetClaudeKeyStatusAsync(Ct);

        status.ShouldBe(ClaudeKeyData.Stored());
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery, sent.Body).ShouldBe((HttpMethod.Get, Route, null));
    }

    [Fact]
    public async Task Store_PutsTheValue_AsTheContractBody()
    {
        _handler.Json(HttpStatusCode.OK, AgentData.Serialize(ClaudeKeyData.Stored()));

        var status = await _client.StoreClaudeKeyAsync(ClaudeKeySentinels.Stored, Ct);

        status.State.ShouldBe(ClaudeKeyStateDto.Stored);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Put, Route));
        sent.Body.ShouldBe($$"""{"apiKey":"{{ClaudeKeySentinels.Stored}}"}""");
    }

    [Fact]
    public async Task Store_MapsTheProblem_WithItsReason()
    {
        _handler.Problem(HttpStatusCode.UnprocessableContent,
            """{"type":"https://stemstudio.local/problems/claude-key-invalid","title":"Not a Claude API key","status":422,"reason":"prefix"}""");

        var ex = await Should.ThrowAsync<ApiException>(() => _client.StoreClaudeKeyAsync("x", Ct));

        ex.Error.Code.ShouldBe(AgentProblemTypes.ClaudeKeyInvalid);
        ex.Error.Extensions["reason"].GetString().ShouldBe(ClaudeKeyInvalidReasons.Prefix);
    }

    // qa F6: a misbehaving server that echoes the key in detail, title, field errors or reason never makes the client's exception
    // (its message, error or extensions) carry it, so no log of the circuit can record it.
    [Theory]
    [InlineData("store")]
    [InlineData("delete")]
    public async Task KeyWriteProblem_EchoingTheKey_IsDroppedFromTheException(string call)
    {
        var echo = ClaudeKeySentinels.Stored;
        _handler.Problem(HttpStatusCode.UnprocessableContent,
            $$"""{"type":"https://stemstudio.local/problems/claude-key-invalid","title":"{{echo}}","status":422,"detail":"{{echo}}","reason":"{{echo}}","errors":{"apiKey":["{{echo}}"]},"value":"{{echo}}"}""");

        var ex = await Should.ThrowAsync<ApiException>(() => call == "store" ? _client.StoreClaudeKeyAsync(echo, Ct) : _client.DeleteClaudeKeyAsync(Ct));

        ex.Error.Code.ShouldBe(AgentProblemTypes.ClaudeKeyInvalid);
        var text = string.Join("|", ex.Message, ex.ToString(), ex.Error.ToString(), ex.Error.UserMessage, string.Join(",", ex.Error.Extensions.Select(pair => pair.Key + "=" + pair.Value)));
        ClaudeKeySentinels.SearchForms(echo).ShouldAllBe(form => !text.Contains(form, StringComparison.Ordinal));
    }

    [Fact]
    public async Task KeyWriteProblem_KeepsAKnownReasonAndTheType()
    {
        _handler.Problem(HttpStatusCode.UnprocessableContent,
            """{"type":"https://stemstudio.local/problems/claude-key-invalid","title":"Not a Claude API key","status":422,"detail":"The key must start with sk-ant-.","reason":"length"}""");

        var ex = await Should.ThrowAsync<ApiException>(() => _client.StoreClaudeKeyAsync("x", Ct));

        (ex.Error.Status, ex.Error.Code, ex.Error.Detail).ShouldBe((HttpStatusCode.UnprocessableContent, AgentProblemTypes.ClaudeKeyInvalid, (string?)null));
        ex.Error.Extensions["reason"].GetString().ShouldBe(ClaudeKeyInvalidReasons.Length);
    }

    [Theory]
    [InlineData(ClaudeKeyTestSourceDto.Active, "active")]
    [InlineData(ClaudeKeyTestSourceDto.Stored, "stored")]
    public async Task TestKey_PostsClaude_WithTheKeySource(ClaudeKeyTestSourceDto source, string wire)
    {
        _handler.Json(HttpStatusCode.OK, """{"backend":"claude","ok":true,"latencyMs":412,"detail":"claude-opus-5-5 available","keySource":"stored"}""");

        var result = await _client.TestClaudeKeyAsync(source, Ct);

        (result.Ok, result.KeySource).ShouldBe((true, ClaudeKeySourceDto.Stored));
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, "/api/agent/settings/test"));
        sent.Body.ShouldBe($$"""{"backend":"claude","claudeKeySource":"{{wire}}"}""");
    }

    [Fact]
    public async Task Delete_ReturnsTheResultingStatus()
    {
        _handler.Json(HttpStatusCode.OK, AgentData.Serialize(ClaudeKeyData.NotConfigured()));

        var status = await _client.DeleteClaudeKeyAsync(Ct);

        status.State.ShouldBe(ClaudeKeyStateDto.NotConfigured);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery, sent.Body).ShouldBe((HttpMethod.Delete, Route, null));
    }
}
