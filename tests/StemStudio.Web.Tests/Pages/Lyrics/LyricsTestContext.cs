using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Lyrics;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages.Lyrics;

/// <summary>Lyrics card fixture: <c>GetLyricsAsync</c> answers the given states in order, the last one repeats.</summary>
public abstract class LyricsTestContext : PageTestContext
{
    protected IRenderedComponent<LyricsSection> RenderSection(LyricsDto first, params LyricsDto[] next) =>
        RenderSection(TestData.Track(), first, next);

    protected IRenderedComponent<LyricsSection> RenderSection(TrackDto track, LyricsDto first, params LyricsDto[] next)
    {
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(first, next);
        return Render<LyricsSection>(p => p.Add(c => c.Track, track));
    }

    protected IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, LyricsDto lyrics)
    {
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(lyrics);
        return RenderAtLevel<LyricsSection>(level, s => s.Add(x => x.Track, TestData.Track()));
    }
}
