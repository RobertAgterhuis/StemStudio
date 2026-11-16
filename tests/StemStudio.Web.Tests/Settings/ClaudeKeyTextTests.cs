using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Settings;

/// <summary>The status texts of the Claude key, built only from the status members (011 follow-up claude-key US2, US5).</summary>
public sealed class ClaudeKeyTextTests
{
    [Fact]
    public void UnknownSaveTime_IsSaid()
    {
        ClaudeKeyText.Status(ClaudeKeyData.Stored() with { StoredAt = null }).ShouldContain("saved on at an unknown time");
    }

    [Fact]
    public void UnavailableStore_WithoutAReason_StillSaysWhatWorks()
    {
        var text = ClaudeKeyText.Status(ClaudeKeyData.StoreUnavailable() with { Reason = null });

        text.ShouldContain("The key store is not available");
        text.ShouldContain("ANTHROPIC_API_KEY environment variable still works");
    }

    [Fact]
    public void Unreadable_ShowsTheStoredLastFour()
    {
        ClaudeKeyText.Status(ClaudeKeyData.Unreadable()).ShouldContain($"...{ClaudeKeyData.StoredFour}) cannot be read");
    }

    [Fact]
    public void UnreadableReason_UsesTheApiText_OrTheFixedOne()
    {
        ClaudeKeyText.UnreadableReason(ClaudeKeyData.Unreadable() with { Reason = "From the API." }).ShouldBe("From the API.");
        ClaudeKeyText.UnreadableReason(ClaudeKeyData.Unreadable() with { Reason = null }).ShouldContain("cannot be read on this installation");
    }

    [Theory]
    [InlineData(ClaudeKeySourceDto.Environment, "key from ANTHROPIC_API_KEY")]
    [InlineData(ClaudeKeySourceDto.Stored, "stored key")]
    public void AcceptedResult_NamesTheTestedKey(ClaudeKeySourceDto source, string name)
    {
        var result = new BackendTestResultDto { Backend = AgentBackendDto.Claude, Ok = true, LatencyMs = 9, Detail = "ok", KeySource = source };

        ClaudeKeyText.TestResult(result).ShouldBe($"Anthropic accepted the {name} (9 ms).");
    }
}
