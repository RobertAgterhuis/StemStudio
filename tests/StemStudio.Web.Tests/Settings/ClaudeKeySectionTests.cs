using System.Net;
using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;
using StemStudio.Web.Components.Settings;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using Cut = Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>;

namespace StemStudio.Web.Tests.Settings;

/// <summary>
/// The Claude key part of the settings page (011 follow-up claude-key US1/US2, CK-FR-014, ui-test-ids 1.21.0
/// settings-agent-claude-key-*): status, password field, save and readable errors that never repeat the value.
/// </summary>
public sealed class ClaudeKeySectionTests : PageTestContext
{
    public ClaudeKeySectionTests() => AgentApi.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Settings());

    private static IElement Id(Cut cut, string testId) => cut.Find($"[data-testid={testId}]");

    private static bool Has(Cut cut, string testId) => cut.FindAll($"[data-testid={testId}]").Count > 0;

    private void KeyIs(ClaudeKeyStatusDto key) => AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.AgentStatus(key));

    private Cut RenderSettings(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var cut = RenderAtLevel<SettingsPage>(level, _ => { });
        cut.WaitForElement("[data-testid=settings-agent-claude-key]");
        return cut;
    }

    private Cut Typed(string value)
    {
        var cut = RenderSettings();
        Id(cut, "settings-agent-claude-key-input").Input(value);
        return cut;
    }

    public static TheoryData<string, string, string, string> States() => new()
    {
        { "not-configured", "not-configured", "", "absent" },
        { "environment", "environment", "environment", "absent" },
        { "environment+stored", "environment", "environment", "readable" },
        { "environment+unreadable", "environment", "environment", "unreadable" },
        { "stored", "stored", "stored", "readable" },
        { "unreadable", "stored-unreadable", "", "unreadable" },
    };

    private static ClaudeKeyStatusDto Named(string name) => name switch
    {
        "environment" => ClaudeKeyData.Environment(),
        "environment+stored" => ClaudeKeyData.Environment(StoredClaudeKeyStateDto.Readable),
        "environment+unreadable" => ClaudeKeyData.Environment(StoredClaudeKeyStateDto.Unreadable),
        "stored" => ClaudeKeyData.Stored(),
        "unreadable" => ClaudeKeyData.Unreadable(),
        _ => ClaudeKeyData.NotConfigured(),
    };

    [Theory]
    [MemberData(nameof(States))]
    public void Status_CarriesStateSourceAndStored_AsALiveRegion(string name, string state, string source, string stored)
    {
        KeyIs(Named(name));

        var status = Id(RenderSettings(), "settings-agent-claude-key-status");

        (status.GetAttribute("data-state"), status.GetAttribute("data-source"), status.GetAttribute("data-stored")).ShouldBe((state, source, stored));
        status.GetAttribute("aria-live").ShouldBe("polite");
    }

    [Fact]
    public void Group_IsLabelledByItsHeading()
    {
        var cut = RenderSettings();

        var group = Id(cut, "settings-agent-claude-key");
        group.GetAttribute("role").ShouldBe("group");
        cut.Find($"#{group.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Claude API key");
    }

    [Fact]
    public void NoKey_SaysSo_AndKeepsTheEnvironmentAlternative()
    {
        var text = Id(RenderSettings(), "settings-agent-claude-key-status").TextContent;

        text.ShouldContain("No key");
        text.ShouldContain("ANTHROPIC_API_KEY");
    }

    [Fact]
    public void EnvironmentKey_NamesTheVariable_AndItsLastFour_AndStillOffersTheField()
    {
        KeyIs(ClaudeKeyData.Environment());

        var cut = RenderSettings();

        Id(cut, "settings-agent-claude-key-status").TextContent.ShouldContain($"From the environment (ANTHROPIC_API_KEY), ends with ...{ClaudeKeyData.EnvFour}");
        Has(cut, "settings-agent-claude-key-input").ShouldBeTrue();
        Has(cut, "settings-agent-claude-key-env-override").ShouldBeFalse();
    }

    [Fact]
    public void StoredKey_ShowsLastFourAndSaveTime_AndThatItIsEncrypted()
    {
        KeyIs(ClaudeKeyData.Stored());

        var text = Id(RenderSettings(), "settings-agent-claude-key-status").TextContent;

        text.ShouldContain($"Stored key, ends with ...{ClaudeKeyData.StoredFour}, saved on 2026-11-16 15:40 UTC");
        text.ShouldContain("stored encrypted on this computer");
    }

    [Theory]
    [InlineData(StoredClaudeKeyStateDto.Readable)]
    [InlineData(StoredClaudeKeyStateDto.Unreadable)]
    public void EnvironmentAndStoredKey_ShowTheOverrideNote(StoredClaudeKeyStateDto stored)
    {
        KeyIs(ClaudeKeyData.Environment(stored));

        var cut = RenderSettings();

        var note = Id(cut, "settings-agent-claude-key-env-override").TextContent;
        note.ShouldContain($"A saved key exists (ends with ...{ClaudeKeyData.StoredFour}, saved 2026-11-16 15:40 UTC)");
        note.ShouldContain("not used while ANTHROPIC_API_KEY is set");
        Id(cut, "settings-agent-claude-key-status").TextContent.ShouldContain($"...{ClaudeKeyData.EnvFour}");
    }

    [Theory]
    [InlineData("not-configured")]
    [InlineData("stored")]
    [InlineData("unreadable")]
    public void WithoutTheEnvironmentKey_ThereIsNoOverrideNote(string name)
    {
        KeyIs(Named(name));

        Has(RenderSettings(), "settings-agent-claude-key-env-override").ShouldBeFalse();
    }

    [Fact]
    public void Input_IsAnEmptyPasswordField_WithoutAutocomplete()
    {
        var cut = RenderSettings();

        var input = Id(cut, "settings-agent-claude-key-input");
        input.GetAttribute("type").ShouldBe("password");
        input.GetAttribute("autocomplete").ShouldBe("off");
        input.GetAttribute("spellcheck").ShouldBe("false");
        input.GetAttribute("maxlength").ShouldBe("512");
        (input.GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        cut.Find($"label[for={input.Id}]").TextContent.ShouldBe("Claude API key");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Save_IsDisabled_WhileTheInputIsEmpty(string value)
    {
        var cut = Typed(value);

        var save = Id(cut, "settings-agent-claude-key-save");
        save.TextContent.ShouldContain("Save key");
        save.HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Save_StoresTheKey_EmptiesTheField_ShowsTheStatus_AndNeverRendersTheValue()
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.Stored());
        var cut = Typed(ClaudeKeySentinels.Stored);
        Id(cut, "settings-agent-claude-key-save").HasAttribute("disabled").ShouldBeFalse();

        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("stored"));
        AgentApi.Received(1).StoreClaudeKeyAsync(ClaudeKeySentinels.Stored, Arg.Any<CancellationToken>());
        Has(cut, "settings-agent-claude-key-input").ShouldBeFalse();
        Id(cut, "settings-agent-claude-status").GetAttribute("data-key-configured").ShouldBe("true");
        Id(cut, "settings-agent-backend-claude").HasAttribute("disabled").ShouldBeFalse();
        foreach (var form in ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored))
        {
            cut.Markup.ShouldNotContain(form);
        }
    }

    [Fact]
    public void Save_DoesNotChangeTheBackend()
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ClaudeKeyData.Stored());
        var cut = Typed(ClaudeKeySentinels.Stored);

        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("stored"));
        AgentApi.DidNotReceive().UpdateSettingsAsync(Arg.Any<UpdateAgentSettingsRequest>(), Arg.Any<CancellationToken>());
        AgentApi.DidNotReceive().TestBackendAsync(Arg.Any<AgentBackendDto>(), Arg.Any<CancellationToken>());
        Id(cut, "settings-agent-backend-ollama").HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public void Save_IsDisabled_WhileSaving()
    {
        var pending = new TaskCompletionSource<ClaudeKeyStatusDto>();
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var cut = Typed(ClaudeKeySentinels.Stored);

        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-save").HasAttribute("disabled").ShouldBeTrue());
        Id(cut, "settings-agent-claude-key-input").Input(ClaudeKeySentinels.Replacement);
        Id(cut, "settings-agent-claude-key-save").HasAttribute("disabled").ShouldBeTrue();
        pending.SetResult(ClaudeKeyData.Stored());
        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("stored"));
    }

    public static TheoryData<HttpStatusCode, string, string?, string> Failures() => new()
    {
        { HttpStatusCode.UnprocessableContent, AgentProblemTypes.ClaudeKeyInvalid, ClaudeKeyInvalidReasons.Empty, "Enter the key" },
        { HttpStatusCode.UnprocessableContent, AgentProblemTypes.ClaudeKeyInvalid, ClaudeKeyInvalidReasons.Prefix, "must start with" },
        { HttpStatusCode.UnprocessableContent, AgentProblemTypes.ClaudeKeyInvalid, ClaudeKeyInvalidReasons.Length, "40 to 256 characters" },
        { HttpStatusCode.UnprocessableContent, AgentProblemTypes.ClaudeKeyInvalid, ClaudeKeyInvalidReasons.Characters, "only letters, digits" },
        { HttpStatusCode.UnprocessableContent, AgentProblemTypes.ClaudeKeyInvalid, ClaudeKeyInvalidReasons.AdminKey, "admin key" },
        { HttpStatusCode.UnprocessableContent, AgentProblemTypes.ClaudeKeyInvalid, null, "not a Claude API key" },
        { HttpStatusCode.ServiceUnavailable, AgentProblemTypes.KeyStoreUnavailable, null, "key store is not available" },
        { HttpStatusCode.RequestEntityTooLarge, "payload-too-large", null, "too long" },
        { HttpStatusCode.BadRequest, AgentProblemTypes.InvalidBody, null, "could not be sent" },
        { HttpStatusCode.InternalServerError, "internal", null, "(500)" },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void SaveFailure_ShowsAReadableError_EmptiesTheField_AndNeverRepeatsTheValue(HttpStatusCode status, string code, string? reason, string text)
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ClaudeKeyStatusDto>(_ => throw ClaudeKeyData.Problem(status, code, detail: ClaudeKeySentinels.Stored, reason: reason));
        var cut = Typed(ClaudeKeySentinels.Stored);

        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-error").TextContent.ShouldContain(text));
        Id(cut, "settings-agent-claude-key-error").GetAttribute("role").ShouldBe("alert");
        (Id(cut, "settings-agent-claude-key-input").GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        Id(cut, "settings-agent-claude-key-status").GetAttribute("data-state").ShouldBe("not-configured");
        foreach (var form in ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored))
        {
            cut.Markup.ShouldNotContain(form);
        }
    }

    [Fact]
    public void UnreachableApi_SaysSo()
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ClaudeKeyStatusDto>(_ => throw new StemStudio.Web.Api.ApiException(StemStudio.Web.Api.ApiError.Unreachable));
        var cut = Typed(ClaudeKeySentinels.Stored);

        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Id(cut, "settings-agent-claude-key-error").TextContent.ShouldContain("cannot be reached"));
    }

    [Fact]
    public void ASuccessfulSave_ClearsAnEarlierError()
    {
        AgentApi.StoreClaudeKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            _ => throw ClaudeKeyData.Problem(HttpStatusCode.UnprocessableContent, AgentProblemTypes.ClaudeKeyInvalid, reason: ClaudeKeyInvalidReasons.Prefix),
            _ => Task.FromResult(ClaudeKeyData.Stored()));
        var cut = Typed("not-a-key");
        Id(cut, "settings-agent-claude-key-save").Click();
        cut.WaitForAssertion(() => Has(cut, "settings-agent-claude-key-error").ShouldBeTrue());

        Id(cut, "settings-agent-claude-key-input").Input(ClaudeKeySentinels.Stored);
        Id(cut, "settings-agent-claude-key-save").Click();

        cut.WaitForAssertion(() => Has(cut, "settings-agent-claude-key-error").ShouldBeFalse());
    }

    [Fact]
    public void UnavailableKeyStore_DisablesSaving_AndSaysWhy()
    {
        KeyIs(ClaudeKeyData.StoreUnavailable());
        var cut = Typed(ClaudeKeySentinels.Stored);

        Id(cut, "settings-agent-claude-key-save").HasAttribute("disabled").ShouldBeTrue();
        Id(cut, "settings-agent-claude-key-status").TextContent.ShouldContain("The key store is not available");
    }

    [Fact]
    public void Beginner_SeesTheKeySection()
    {
        var cut = RenderSettings(ExperienceLevel.Beginner);

        Has(cut, "settings-agent-claude-key-status").ShouldBeTrue();
        Has(cut, "settings-agent-claude-key-input").ShouldBeTrue();
    }
}
