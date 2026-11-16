using System.Text.Json;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Agent;

/// <summary>Which step events are agent saves of the open mixer (011 O3).</summary>
public sealed class AgentSavedMixTests
{
    private static readonly AgentRunView Run = AgentRunView.Start(AgentData.RunId) with { Steps = [AgentData.Step(1, "save_mix_as")] };

    private static AgentEvent Updated(string? summary, Guid? stepId = null, AgentStepStatusDto status = AgentStepStatusDto.Succeeded) =>
        new(1, AgentEventTypes.StepUpdated, JsonSerializer.SerializeToElement(
            new StepUpdatedEventDto { StepId = stepId ?? AgentData.StepId, Status = status, ResultSummary = summary is null ? null : AgentData.Json(summary) },
            JsonSerializerOptions.Web));

    [Fact]
    public void SucceededSave_GivesTheStepAndTheMix() =>
        AgentSavedMix.From(Run, Updated($$"""{"mixId":"{{AgentData.MixId}}"}""")).ShouldBe((AgentData.StepId, AgentData.MixId));

    [Theory]
    [InlineData("""{"mixId":"not-a-guid"}""")]
    [InlineData("""{"mixId":42}""")]
    [InlineData("""["mixId"]""")]
    public void SummaryWithoutAMixId_IsNoSave(string summary) => AgentSavedMix.From(Run, Updated(summary)).ShouldBeNull();

    [Fact]
    public void UnknownStep_IsNoSave() =>
        AgentSavedMix.From(Run, Updated($$"""{"mixId":"{{AgentData.MixId}}"}""", Guid.NewGuid())).ShouldBeNull();

    [Fact]
    public void OtherEvent_IsNoSave() =>
        AgentSavedMix.From(Run, new AgentEvent(1, AgentEventTypes.TextDelta, AgentData.Json("""{"text":"Saved."}"""))).ShouldBeNull();
}
