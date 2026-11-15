using Bunit;
using NSubstitute;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Generation;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>Feature 009 fixture: the generator is ready and restyles answer a queued generation.</summary>
public abstract class GenerationTestContext : PageTestContext
{
    protected GenerationTestContext()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.Status());
        Api.CreateRestyleAsync(Arg.Any<Guid>(), Arg.Any<RestyleRequest>(), Arg.Any<CancellationToken>())
            .Returns(GenerationData.Generation(GenerationStateDto.Queued));
    }

    protected bool Closed { get; private set; }

    protected GenerationDto? Started { get; private set; }

    protected RestyleRequest SentRestyle() =>
        (RestyleRequest)Api.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IStemStudioApiClient.CreateRestyleAsync)).GetArguments()[1]!;

    protected IRenderedComponent<RestyleDialog> RenderDialog(TrackDto? track = null) =>
        Render<RestyleDialog>(p => p
            .Add(c => c.Track, track ?? TestData.Track())
            .Add(c => c.OnClose, () => Closed = true)
            .Add(c => c.OnStarted, g => Started = g));

    protected IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderDialogAt(ExperienceLevel level) =>
        RenderAtLevel<RestyleDialog>(level, p => p.Add(c => c.Track, TestData.Track()));

    protected IRenderedComponent<GenerationList> RenderList(params GenerationDto[] generations)
    {
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns(generations);
        return Render<GenerationList>(p => p.Add(c => c.Query, new GenerationQuery(null, TestData.TrackId)));
    }
}
