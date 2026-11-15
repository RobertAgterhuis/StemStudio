using Bunit;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Track detail page fixture: <c>GetTrackAsync</c> answers the given states in order, the last one repeats.</summary>
public abstract class TrackDetailTestContext : PageTestContext
{
    protected IRenderedComponent<TrackDetail> RenderPage(TrackDto first, params TrackDto[] next)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(first, next);
        return Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    protected static async Task ConfirmDeleteAsync(IRenderedComponent<TrackDetail> page)
    {
        await page.Find("[data-testid=track-delete]").ClickAsync(new());
        await page.Find("[data-testid=track-delete-confirm]").ClickAsync(new());
    }
}
