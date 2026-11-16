using System.Net;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Components.Settings;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Settings;

/// <summary>"Test key" on explicit request only (011 follow-up claude-key US4, CK-FR-007; ui-test-ids 1.21.0).</summary>
public sealed class ClaudeKeyTestTests : PageTestContext
{
    public ClaudeKeyTestTests()
    {
        AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings());
        KeyIs(ClaudeKeyData.Stored());
    }

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private static bool Has(Cut cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    private static BackendTestResultDto Result(bool ok, ClaudeKeySourceDto source, string detail = "claude-opus-5-5 available") =>
        new() { Backend = AgentBackendDto.Claude, Ok = ok, LatencyMs = 412, Detail = detail, KeySource = source };

    private void KeyIs(ClaudeKeyStatusDto key) => AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.AgentStatus(key));

    private void Answers(BackendTestResultDto result) =>
        AgentApi.TestClaudeKeyAsync(Arg.Any<ClaudeKeyTestSourceDto>(), Arg.Any<CancellationToken>()).Returns(result);

    private Cut RenderSettings()
    {
        var cut = RenderAtLevel<SettingsPage>(ExperienceLevel.Advanced, _ => { });
        cut.WaitForElement("[data-testid=settings-agent-claude-key]");
        return cut;
    }

    [Theory]
    [InlineData("environment", true)]
    [InlineData("environment+stored", true)]
    [InlineData("environment+unreadable", true)]
    [InlineData("stored", true)]
    [InlineData("not-configured", false)]
    [InlineData("unreadable", false)]
    public void TestKey_IsOffered_OnlyWithAUsableKey(string name, bool offered)
    {
        KeyIs(name switch
        {
            "environment" => ClaudeKeyData.Environment(),
            "environment+stored" => ClaudeKeyData.Environment(StoredClaudeKeyStateDto.Readable),
            "environment+unreadable" => ClaudeKeyData.Environment(StoredClaudeKeyStateDto.Unreadable),
            "stored" => ClaudeKeyData.Stored(),
            "unreadable" => ClaudeKeyData.Unreadable(),
            _ => ClaudeKeyData.NotConfigured(),
        });

        var cut = RenderSettings();

        Has(cut, "settings-agent-claude-key-test").ShouldBe(offered);
    }

    [Fact]
    public void LoadingThePage_TestsNothing()
    {
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test").TextContent.ShouldContain("Test key");
        Has(cut, "settings-agent-claude-key-test-result").ShouldBeFalse();
        AgentApi.DidNotReceive().TestClaudeKeyAsync(Arg.Any<ClaudeKeyTestSourceDto>(), Arg.Any<CancellationToken>());
        AgentApi.DidNotReceive().TestBackendAsync(Arg.Any<AgentBackendDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TestKey_TestsTheActiveKey_Once_AndShowsTheResult()
    {
        Answers(Result(true, ClaudeKeySourceDto.Stored));
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test").Click();

        cut.WaitForAssertion(() => Has(cut, "settings-agent-claude-key-test-result").ShouldBeTrue());
        AgentApi.Received(1).TestClaudeKeyAsync(ClaudeKeyTestSourceDto.Active, Arg.Any<CancellationToken>());
        var result = Id(cut, "settings-agent-claude-key-test-result");
        (result.GetAttribute("data-ok"), result.GetAttribute("data-key-source")).ShouldBe(("true", "stored"));
        result.TextContent.ShouldContain("Anthropic accepted the stored key (412 ms)");
        result.GetAttribute("role").ShouldBe("status");
    }

    [Fact]
    public void ARejectedKey_ShowsTheReadableFailure()
    {
        Answers(Result(false, ClaudeKeySourceDto.Environment, "Anthropic rejected the key (401). Replace it."));
        KeyIs(ClaudeKeyData.Environment());
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-test-result").GetAttribute("data-ok").ShouldBe("false"));
        var result = Id(cut, "settings-agent-claude-key-test-result");
        result.GetAttribute("data-key-source").ShouldBe("environment");
        result.TextContent.ShouldContain("Anthropic rejected the key (401). Replace it.");
    }

    [Fact]
    public void ResultWithoutASource_HasAnEmptySourceAttribute()
    {
        Answers(Result(true, ClaudeKeySourceDto.Stored) with { KeySource = null });
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-test-result").GetAttribute("data-key-source").ShouldBe(string.Empty));
        Id(cut, "settings-agent-claude-key-test-result").TextContent.ShouldContain("Anthropic accepted the key (412 ms)");
    }

    [Fact]
    public void OneSource_HasNoSourceChoice()
    {
        Has(RenderSettings(), "settings-agent-claude-key-test-source").ShouldBeFalse();
    }

    [Fact]
    public void BothKeys_OfferTheSourceChoice_AndTestTheStoredKeyOnRequest()
    {
        KeyIs(ClaudeKeyData.Environment(StoredClaudeKeyStateDto.Readable));
        Answers(Result(true, ClaudeKeySourceDto.Stored));
        var cut = RenderSettings();

        var group = Id(cut, "settings-agent-claude-key-test-source");
        group.GetAttribute("role").ShouldBe("radiogroup");
        Id(cut, "settings-agent-claude-key-test-source-active").HasAttribute("checked").ShouldBeTrue();
        Id(cut, "settings-agent-claude-key-test-source-stored").Change("stored");
        Id(cut, "settings-agent-claude-key-test").Click();

        cut.WaitForAssertion(() => AgentApi.Received(1).TestClaudeKeyAsync(ClaudeKeyTestSourceDto.Stored, Arg.Any<CancellationToken>()));
        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-test-result").GetAttribute("data-key-source").ShouldBe("stored"));
    }

    [Fact]
    public void ChoosingActiveAgain_TestsTheActiveKey()
    {
        KeyIs(ClaudeKeyData.Environment(StoredClaudeKeyStateDto.Readable));
        Answers(Result(true, ClaudeKeySourceDto.Environment));
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test-source-stored").Change("stored");
        Id(cut, "settings-agent-claude-key-test-source-active").Change("active");
        Id(cut, "settings-agent-claude-key-test").Click();

        cut.WaitForAssertion(() => AgentApi.Received(1).TestClaudeKeyAsync(ClaudeKeyTestSourceDto.Active, Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void TestKey_IsDisabled_WhileTesting()
    {
        var pending = new TaskCompletionSource<BackendTestResultDto>();
        AgentApi.TestClaudeKeyAsync(Arg.Any<ClaudeKeyTestSourceDto>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-test").HasAttribute("disabled").ShouldBeTrue());
        pending.SetResult(Result(true, ClaudeKeySourceDto.Stored));
        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-test").HasAttribute("disabled").ShouldBeFalse());
    }

    [Fact]
    public void LocalOnly_DisablesTheTestAndTheSourceChoice()
    {
        KeyIs(ClaudeKeyData.Environment(StoredClaudeKeyStateDto.Readable));
        Services.AddScoped(_ => new PageOrigin(new TestNavigationManager("http://192.168.1.20:8080/")));

        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test").HasAttribute("disabled").ShouldBeTrue();
        Id(cut, "settings-agent-claude-key-test-source-active").HasAttribute("disabled").ShouldBeTrue();
        Id(cut, "settings-agent-claude-key-test-source-stored").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void ALocalOnlyAnswer_ShowsTheNotice()
    {
        AgentApi.TestClaudeKeyAsync(Arg.Any<ClaudeKeyTestSourceDto>(), Arg.Any<CancellationToken>())
            .Returns<BackendTestResultDto>(_ => throw ClaudeKeyData.Problem(HttpStatusCode.Forbidden, AgentProblemTypes.LocalOnly));
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test").Click();

        cut.WaitForAssertion(() => Has(cut, "settings-agent-claude-key-local-only").ShouldBeTrue());
        Id(cut, "settings-agent-claude-key-test").HasAttribute("disabled").ShouldBeTrue();
        Has(cut, "settings-agent-claude-key-test-result").ShouldBeFalse();
    }

    [Fact]
    public void AFailedTestCall_ShowsTheError()
    {
        AgentApi.TestClaudeKeyAsync(Arg.Any<ClaudeKeyTestSourceDto>(), Arg.Any<CancellationToken>())
            .Returns<BackendTestResultDto>(_ => throw ClaudeKeyData.Problem(HttpStatusCode.InternalServerError, "internal"));
        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-test").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-error").TextContent.ShouldContain("(500)"));
    }

    [Fact]
    public void AKeyChange_ClearsTheOldTestResult()
    {
        Answers(Result(true, ClaudeKeySourceDto.Stored));
        AgentApi.DeleteClaudeKeyAsync(Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.Environment());
        var cut = RenderSettings();
        Id(cut, "settings-agent-claude-key-test").Click();
        cut.WaitForAssertion(() => Has(cut, "settings-agent-claude-key-test-result").ShouldBeTrue());

        Id(cut, "settings-agent-claude-key-delete").Click();
        Id(cut, "settings-agent-claude-key-delete-confirm-yes").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("environment"));
        Has(cut, "settings-agent-claude-key-test-result").ShouldBeFalse();
    }
}
