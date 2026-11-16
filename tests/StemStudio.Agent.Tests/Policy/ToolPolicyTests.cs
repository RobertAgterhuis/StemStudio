using System.Text.Json;
using StemStudio.Agent.Model;
using StemStudio.Agent.Policy;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Policy;

/// <summary>T016: the deterministic policy gate (data-model 6; FR-012..FR-015, FR-025).</summary>
public sealed class ToolPolicyTests
{
    private const string TrackA = "11111111-1111-1111-1111-111111111111";
    private const string TrackB = "22222222-2222-2222-2222-222222222222";
    private const string Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    public enum UrlCase
    {
        NoUrl,
        FromUser,
        NotFromUser,
    }

    public static TheoryData<ToolRiskDto, bool, UrlCase, bool> Combinations()
    {
        var data = new TheoryData<ToolRiskDto, bool, UrlCase, bool>();
        foreach (var risk in Enum.GetValues<ToolRiskDto>())
        {
            foreach (var tainted in new[] { false, true })
            {
                foreach (var url in Enum.GetValues<UrlCase>())
                {
                    foreach (var planCovered in new[] { false, true })
                    {
                        data.Add(risk, tainted, url, planCovered);
                    }
                }
            }
        }

        return data;
    }

    private static AgentToolDescriptor Tool(ToolRiskDto risk, bool hasUrl) => new()
    {
        Name = "tool_x",
        Feature = "001",
        Kind = ToolKindDto.Server,
        Risk = risk,
        UndoKind = UndoKindDto.None,
        Group = ToolGroup.Core,
        Availability = ToolAvailability.Always,
        Title = "Tool X",
        Description = "x",
        Schema = ToolSchema.Empty,
        UrlArguments = hasUrl ? ["url"] : [],
        TargetArgument = "trackId",
    };

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private static PlanItem Item(string tool) => new() { Index = 0, Title = "step", Tool = tool, Status = PlanItemStatusDto.Pending };

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Decide_FollowsTheRiskTaintProvenanceAndPlanScopeTable(ToolRiskDto risk, bool tainted, UrlCase url, bool planCovered)
    {
        var arguments = url == UrlCase.NoUrl
            ? Json(new { trackId = TrackA })
            : Json(new { trackId = TrackA, url = url == UrlCase.FromUser ? Url : "https://evil.example/x" });
        var input = new PolicyInput
        {
            Tool = Tool(risk, url != UrlCase.NoUrl),
            Arguments = arguments,
            Tainted = tainted,
            UserMessages = [$"Importeer {Url} graag"],
            Plan = [Item("tool_x")],
            PlanGrants = planCovered ? [new PlanHeavyGrant("tool_x", TrackA)] : [],
        };

        var decision = ToolPolicy.Decide(input);

        var expected = new List<string>();
        if (risk == ToolRiskDto.Heavy)
        {
            expected.Add(AgentDecisionReasons.RiskHeavy);
        }

        if (risk == ToolRiskDto.Destructive)
        {
            expected.Add(AgentDecisionReasons.RiskDestructive);
        }

        var changes = risk is not ToolRiskDto.Read;
        if (changes && tainted)
        {
            expected.Add(AgentDecisionReasons.Tainted);
        }

        if (changes && url == UrlCase.NotFromUser)
        {
            expected.Add(AgentDecisionReasons.UrlNotFromUser);
        }

        var coveredByPlan = planCovered && expected.SequenceEqual([AgentDecisionReasons.RiskHeavy]);
        var auto = expected.Count == 0 || coveredByPlan;
        decision.Decision.ShouldBe(auto ? StepDecisionDto.Auto : StepDecisionDto.ApprovalRequired);
        decision.Reasons.ShouldBe(auto ? [] : expected);
        decision.Refusal.ShouldBeNull();
    }

    [Fact]
    public void ReadTool_WithAnUnknownUrl_StaysAuto()
    {
        var input = new PolicyInput { Tool = Tool(ToolRiskDto.Read, true), Arguments = Json(new { url = "https://x.example" }), Tainted = true, UserMessages = [] };

        ToolPolicy.Decide(input).Decision.ShouldBe(StepDecisionDto.Auto);
    }

    [Fact]
    public void MetaTool_IsAlwaysAuto()
    {
        var input = new PolicyInput { Tool = Tool(ToolRiskDto.Read, false) with { Kind = ToolKindDto.Meta }, Arguments = Json(new { }), Tainted = true, UserMessages = [] };

        ToolPolicy.Decide(input).Decision.ShouldBe(StepDecisionDto.Auto);
    }

    private static PolicyInput HeavyCall(object arguments) => new()
    {
        Tool = Tool(ToolRiskDto.Heavy, false),
        Arguments = Json(arguments),
        Tainted = false,
        UserMessages = [],
        Plan = [Item("tool_x"), Item("other_heavy")],
        PlanGrants = [new PlanHeavyGrant("tool_x", TrackA)],
    };

    [Fact]
    public void PlanScope_CoversTheSameToolAndTarget()
    {
        ToolPolicy.Decide(HeavyCall(new { trackId = TrackA })).Decision.ShouldBe(StepDecisionDto.Auto);
    }

    [Fact]
    public void PlanScope_CoversOnlyTheSameTargetAndAPlannedTool()
    {
        var otherTarget = HeavyCall(new { trackId = TrackB });
        var notPlanned = HeavyCall(new { trackId = TrackA }) with { Plan = [Item("other_tool")] };

        ToolPolicy.Decide(otherTarget).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
        ToolPolicy.Decide(notPlanned).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
    }

    [Fact]
    public void PlanScope_IsBoundToTheApprovedTool_NotAnyPlannedHeavyTool()
    {
        var input = HeavyCall(new { trackId = TrackA }) with { Tool = Tool(ToolRiskDto.Heavy, false) with { Name = "other_heavy" } };

        ToolPolicy.Decide(input).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
    }

    [Theory]
    [MemberData(nameof(SmuggledTargets))]
    public void PlanScope_RejectsATargetThatIsNotExactlyOneString(string argumentsJson)
    {
        var input = HeavyCall(new { }) with { Arguments = JsonDocument.Parse(argumentsJson).RootElement.Clone() };

        ToolPolicy.Decide(input).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
    }

    public static TheoryData<string> SmuggledTargets() => new()
    {
        $$"""{"trackId":["{{TrackA}}","{{TrackB}}"]}""",
        $$"""{"trackId":["{{TrackA}}"]}""",
        """{"trackId":42}""",
        """{"trackId":null}""",
        """{"trackId":{"id":"x"}}""",
        """{}""",
    };

    [Fact]
    public void PlanScope_NeverCoversTargetlessHeavySteps()
    {
        var targetless = Tool(ToolRiskDto.Heavy, false) with { TargetArgument = null };
        var nullGrant = HeavyCall(new { }) with { Tool = targetless, PlanGrants = [new PlanHeavyGrant("tool_x", null)] };
        var nullTargetArgument = HeavyCall(new { }) with { PlanGrants = [new PlanHeavyGrant("tool_x", null)] };

        ToolPolicy.Decide(nullGrant).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
        ToolPolicy.Decide(nullTargetArgument).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
    }

    [Fact]
    public void PlanScope_InjectedCallOnAnotherTrack_StillNeedsApproval()
    {
        // An adversarial model that saw an approved plan for track A tries to restyle track B under the same scope.
        var injected = HeavyCall(new { trackId = TrackB }) with { PlanGrants = [new PlanHeavyGrant("tool_x", TrackA), new PlanHeavyGrant("other_heavy", TrackA)] };

        ToolPolicy.Decide(injected).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
    }

    [Fact]
    public void PlanScope_NeverCoversDestructiveStepsOrCompensations()
    {
        var destructive = new PolicyInput
        {
            Tool = Tool(ToolRiskDto.Destructive, false),
            Arguments = Json(new { trackId = TrackA }),
            Tainted = false,
            UserMessages = [],
            Plan = [Item("tool_x")],
            PlanGrants = [new PlanHeavyGrant("tool_x", TrackA)],
        };
        var compensation = destructive with { Tool = Tool(ToolRiskDto.Heavy, false), IsCompensation = true };

        ToolPolicy.Decide(destructive).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
        var decision = ToolPolicy.Decide(compensation);
        decision.Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
        decision.Reasons.ShouldContain(AgentDecisionReasons.UndoCompensation);
    }

    [Fact]
    public void FreshCompensation_IsAuto_AndStaleCompensationNeedsApproval()
    {
        var input = new PolicyInput { Tool = Tool(ToolRiskDto.Destructive, false), Arguments = Json(new { }), Tainted = false, UserMessages = [], IsCompensation = true, Fresh = true };

        ToolPolicy.Decide(input).Decision.ShouldBe(StepDecisionDto.Auto);
        ToolPolicy.Decide(input with { Fresh = false }).Reasons.ShouldBe([AgentDecisionReasons.RiskDestructive, AgentDecisionReasons.UndoCompensation]);
    }

    [Fact]
    public void ArtistImitation_InFreeText_IsDenied()
    {
        var tool = Tool(ToolRiskDto.Change, false) with { Name = "create_style", ImitationArguments = ["name", "moods", "description"] };
        var input = new PolicyInput { Tool = tool, Arguments = Json(new { name = "Summer", moods = new[] { "happy", "in the style of Coldplay" } }), Tainted = false, UserMessages = [] };

        var decision = ToolPolicy.Decide(input);

        decision.Decision.ShouldBe(StepDecisionDto.Denied);
        decision.Reasons.ShouldBe([AgentDecisionReasons.ArtistImitation]);
        decision.Refusal!.ShouldContain("not artists");
    }

    [Fact]
    public void ArtistImitation_MatchesLibraryArtists_ButNotGenreWords()
    {
        var tool = Tool(ToolRiskDto.Heavy, false) with { Name = "generate_song", ImitationArguments = ["freeCaption"] };
        var input = new PolicyInput { Tool = tool, Arguments = Json(new { freeCaption = "Slow ballad like evil uploader would do" }), Tainted = false, UserMessages = [], LibraryArtists = ["Evil Uploader", "Rock", "X", null] };

        ToolPolicy.Decide(input).Decision.ShouldBe(StepDecisionDto.Denied);
        ToolPolicy.Decide(input with { Arguments = Json(new { freeCaption = "A rock ballad with x factor" }) }).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
    }

    [Fact]
    public void LyricsNotFromUser_IsDenied_AndUserLyricsPass()
    {
        var tool = Tool(ToolRiskDto.Change, false) with { Name = "save_lyrics_sheet", LyricsArguments = ["text"] };
        var message = "Bewaar deze tekst als Zomeravond:\r\n  Zomeravond, de zon gaat onder   \r\n\r\n\r\n\r\nWij blijven nog even\r\n";
        var ok = new PolicyInput { Tool = tool, Arguments = Json(new { text = "Zomeravond, de zon gaat onder\n\n\nWij blijven nog even" }), Tainted = false, UserMessages = ["hoi", message] };
        var invented = ok with { Arguments = Json(new { text = "A verse the model wrote itself" }) };

        ToolPolicy.Decide(ok).Decision.ShouldBe(StepDecisionDto.Auto);
        var denied = ToolPolicy.Decide(invented);
        denied.Decision.ShouldBe(StepDecisionDto.Denied);
        denied.Reasons.ShouldBe([AgentDecisionReasons.LyricsNotFromUser]);
        denied.Refusal!.ShouldContain("your own message");
    }

    [Fact]
    public void LyricsArgument_Absent_IsNotChecked()
    {
        var tool = Tool(ToolRiskDto.Heavy, false) with { LyricsArguments = ["lyricsText"] };
        var input = new PolicyInput { Tool = tool, Arguments = Json(new { lyricsId = TrackA }), Tainted = false, UserMessages = [] };

        ToolPolicy.Decide(input).Decision.ShouldBe(StepDecisionDto.ApprovalRequired);
    }

    [Fact]
    public void Decide_RejectsNullInput()
    {
        Should.Throw<ArgumentNullException>(() => ToolPolicy.Decide(null!));
    }
}
