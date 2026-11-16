using StemStudio.AgentEval.Fixture;

namespace StemStudio.AgentEval.Tests;

/// <summary>Fixture keys map to stable ids; recorded arguments go back to keys, scripts to ids.</summary>
public sealed class FixtureIdsTests
{
    [Fact]
    public void Of_IsStablePerKey_AndDiffersBetweenKeys()
    {
        var ids = new FixtureIds();

        ids.Of("t-hero").ShouldBe(new FixtureIds().Of("t-hero"));
        ids.Of("t-hero").ShouldBe(ids.Of("t-hero"));
        ids.Of("t-hero").ShouldNotBe(ids.Of("t-enter"));
    }

    [Fact]
    public void Register_FixesTheIdOfAKey()
    {
        var ids = new FixtureIds();
        var preset = Guid.NewGuid();

        ids.Register("s-reggae", preset);

        ids.Of("s-reggae").ShouldBe(preset);
    }

    [Fact]
    public void ToKeysAndToIds_RoundTrip_QuotedKeysOnly()
    {
        var ids = new FixtureIds();
        var hero = ids.Of("t-hero");

        ids.ToKeys($$"""{"trackId":"{{hero.ToString().ToUpperInvariant()}}"}""").ShouldBe("""{"trackId":"t-hero"}""");
        ids.ToIds("""{"trackId":"t-hero","name":"t-hero practice"}""").ShouldBe($$"""{"trackId":"{{hero}}","name":"t-hero practice"}""");
        ids.ToKeys(ids.ToIds("""{"trackId":"t-hero"}""")).ShouldBe("""{"trackId":"t-hero"}""");
    }
}
