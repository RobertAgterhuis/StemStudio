using StemStudio.Web.Agent;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Agent;

/// <summary>The page context sent with every command (FR-004; PageContext.page names of backend API 1.10.0).</summary>
public sealed class AgentPageContextTests
{
    private const string Track = "e1b2c3d4-0000-4000-8000-000000000001";
    private const string Mix = "f1b2c3d4-0000-4000-8000-000000000001";

    [Theory]
    [InlineData("", "import")]
    [InlineData("tracks", "tracks")]
    [InlineData("mixes", "mixes")]
    [InlineData("library", "library")]
    [InlineData("styles", "styles")]
    [InlineData("styles/new", "styles")]
    [InlineData("generations", "generations")]
    [InlineData("lyrics", "lyrics")]
    [InlineData("songs/new", "songs")]
    [InlineData("voices", "voices")]
    [InlineData("settings", "settings")]
    [InlineData("something-else", "import")]
    public void ListPages_MapToTheirName_WithoutIds(string path, string page)
    {
        var context = AgentPageContexts.From(new Uri($"http://localhost/{path}"), new Uri("http://localhost/"));

        (context.Page, context.TrackId, context.MixId).ShouldBe((page, null, null));
    }

    [Fact]
    public void TrackPage_CarriesTheTrack()
    {
        var context = AgentPageContexts.From(new Uri($"http://localhost/tracks/{Track}"), new Uri("http://localhost/"));

        (context.Page, context.TrackId, context.MixId).ShouldBe(("track", AgentData.TrackId, null));
    }

    [Theory]
    [InlineData($"tracks/{Track}/mixer", null)]
    [InlineData($"tracks/{Track}/mixer?mixId={Mix}", Mix)]
    [InlineData($"tracks/{Track}/mixer?mixId=nonsense", null)]
    public void MixerPage_CarriesTheTrackAndTheMix(string path, string? mix)
    {
        var context = AgentPageContexts.From(new Uri($"http://localhost/app/{path}"), new Uri("http://localhost/app/"));

        (context.Page, context.TrackId, context.MixId).ShouldBe(("mixer", AgentData.TrackId, mix is null ? null : Guid.Parse(mix)));
    }
}
