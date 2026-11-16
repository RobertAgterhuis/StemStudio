using StemStudio.Agent.Model;
using StemStudio.Agent.Policy;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tests.Policy;

/// <summary>T016: provenance (URL and lyrics), guardrail normalisation and approval binding.</summary>
public sealed class ProvenanceAndBindingTests
{
    [Theory]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC", true)]
    [InlineData("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQ", true)]
    [InlineData("https://open.spotify.com/track/4ULU6hMCjMI75M1A2tKUQC", false)]
    [InlineData("https://open.spotify.com/track/other", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void Url_MustBeALiteralSubstringOfAUserMessage(string url, bool expected)
    {
        ProvenanceChecker.UrlFromUser(url, ["Import this Spotify song: https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC please"]).ShouldBe(expected);
    }

    [Fact]
    public void Url_ComparisonNormalisesWhitespaceOnly()
    {
        ProvenanceChecker.UrlFromUser("  https://x.example/a  ", ["see https://x.example/a"]).ShouldBeTrue();
        ProvenanceChecker.UrlFromUser("https://x.example/a b", ["see https://x.example/a \n\t b"]).ShouldBeTrue();
        ProvenanceChecker.UrlFromUser("https://X.example/a", ["see https://x.example/a"]).ShouldBeFalse();
    }

    [Fact]
    public void Lyrics_AreNormalisedLikeTheBackend()
    {
        var normalized = ProvenanceChecker.NormalizeLyrics("[ar:Someone]\r\n[00:12.30]First line   \r\n\r\n\r\n\r\n\r\nSecond\r\n\r\n");

        normalized.ShouldBe("First line\n\n\nSecond");
        ProvenanceChecker.NormalizeLyrics(null).ShouldBe(string.Empty);
        ProvenanceChecker.LyricsFromUser("", ["anything"]).ShouldBeFalse();
        ProvenanceChecker.LyricsFromUser("Second", ["First line\rSecond"]).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Klinkt als Metallica", true)]
    [InlineData("in de stijl van iemand", true)]
    [InlineData("imitating a singer", true)]
    [InlineData("BEYONCÉ vibes", true)]
    [InlineData("Dreamy synth pop with warm pads", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Imitation_IsDetectedCaseAndDiacriticsInsensitively(string? text, bool expected)
    {
        ImitationGuard.Create([]).IsImitation(text).ShouldBe(expected);
    }

    [Fact]
    public void Imitation_GuardRejectsNullArtists()
    {
        Should.Throw<ArgumentNullException>(() => ImitationGuard.Create(null!));
        ImitationGuard.Normalize("  Hé,  there!! ").ShouldBe("he there");
    }

    [Fact]
    public void ApprovalBinding_RequiresAPendingApprovalWithTheSameHash()
    {
        var approval = new Approval
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            Status = ApprovalStatusDto.Approved,
            Scope = ApprovalScopeDto.Once,
            ArgumentsHash = CanonicalJson.Hash("{}"),
            RequestedAt = DateTimeOffset.UnixEpoch,
        };

        ApprovalBinding.Matches(approval, CanonicalJson.Hash("{}")).ShouldBeTrue();
        ApprovalBinding.Matches(approval, CanonicalJson.Hash("{\"a\":1}")).ShouldBeFalse();
        ApprovalBinding.Matches(approval, CanonicalJson.Hash("{}").ToUpperInvariant()).ShouldBeFalse();
        ApprovalBinding.Matches(approval with { Status = ApprovalStatusDto.Pending }, CanonicalJson.Hash("{}")).ShouldBeFalse();
        Should.Throw<ArgumentNullException>(() => ApprovalBinding.Matches(null!, "x"));
    }

    [Fact]
    public void ApprovalBinding_GrantsPlanScopeOnlyForApprovedPlanHeavyDecisions()
    {
        var once = new Approval
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            Status = ApprovalStatusDto.Approved,
            Scope = ApprovalScopeDto.Once,
            ArgumentsHash = "h",
            RequestedAt = DateTimeOffset.UnixEpoch,
        };
        var target = Guid.NewGuid().ToString();

        ApprovalBinding.Grant(once with { Scope = ApprovalScopeDto.PlanHeavy }, "restyle_track", target).ShouldBe(new PlanHeavyGrant("restyle_track", target));
        ApprovalBinding.Grant(once with { Scope = ApprovalScopeDto.PlanHeavy }, "restyle_track", null).ShouldBeNull();
        ApprovalBinding.Grant(once, "restyle_track", target).ShouldBeNull();
        ApprovalBinding.Grant(once with { Scope = ApprovalScopeDto.PlanHeavy, Status = ApprovalStatusDto.Rejected }, "restyle_track", target).ShouldBeNull();
    }

    [Fact]
    public void PolicyDecision_Auto_HasNoReasons()
    {
        PolicyDecision.Auto.Decision.ShouldBe(StepDecisionDto.Auto);
        PolicyDecision.Auto.Reasons.ShouldBeEmpty();
    }
}
