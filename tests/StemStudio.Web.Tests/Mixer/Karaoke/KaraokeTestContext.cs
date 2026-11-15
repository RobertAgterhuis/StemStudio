using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Audio;
using StemStudio.Web.Components.Mixer.Karaoke;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Karaoke;

/// <summary>Karaoke banner fixture: <c>GetLyricsAsync</c> answers the given states in order, the last one repeats.</summary>
public abstract class KaraokeTestContext : PageTestContext
{
    protected IRenderedComponent<KaraokeBanner> RenderBanner(LyricsDto first, params LyricsDto[] next) =>
        RenderBanner(true, first, next);

    protected IRenderedComponent<KaraokeBanner> RenderBanner(bool engineReady, LyricsDto first, params LyricsDto[] next)
    {
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(first, next);
        return Render<KaraokeBanner>(p => p.Add(c => c.TrackId, TestData.TrackId).Add(c => c.EngineReady, engineReady));
    }

    protected IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, LyricsDto lyrics)
    {
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(lyrics);
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<KaraokeBanner>(b => b.Add(x => x.TrackId, TestData.TrackId).Add(x => x.EngineReady, true)));
    }

    protected KaraokeLyrics AttachedLyrics() =>
        (KaraokeLyrics)Engine.ReceivedCalls().Last(c => c.GetMethodInfo().Name == nameof(IAudioEngine.AttachKaraokeAsync)).GetArguments()[1]!;

    protected KaraokeOptions AttachedOptions() =>
        (KaraokeOptions)Engine.ReceivedCalls().Last(c => c.GetMethodInfo().Name == nameof(IAudioEngine.AttachKaraokeAsync)).GetArguments()[2]!;
}
