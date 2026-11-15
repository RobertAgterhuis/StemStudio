using Bunit;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Web.Components.Scores;
using StemStudio.Web.Experience;
using StemStudio.Web.Scores;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Scores;

/// <summary>Sheet music dialog fixture: options per source, creation answers a queued score item.</summary>
public abstract class ScoreTestContext : PageTestContext
{
    protected ScoreTestContext() =>
        Api.CreateScoreAsync(Arg.Any<CreateScoreRequest>(), Arg.Any<CancellationToken>()).Returns(ScoreData.Asset(AssetStatusDto.Queued));

    protected bool Closed { get; private set; }

    protected CreateScoreRequest SentRequest() =>
        (CreateScoreRequest)Api.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(Api.CreateScoreAsync)).GetArguments()[0]!;

    protected IRenderedComponent<ScoreDialog> RenderDialog(ScoreOptionsDto options, ScoreTarget? target = null)
    {
        target ??= ScoreTarget.ForChannel(TestData.Track(), "bass");
        Api.GetScoreOptionsAsync(target.Source, Arg.Any<CancellationToken>()).Returns(options);
        return Render<ScoreDialog>(p => p.Add(c => c.Target, target).Add(c => c.OnClose, () => Closed = true));
    }

    protected IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderDialogAt(
        ExperienceLevel level, ScoreOptionsDto options, ScoreTarget? target = null)
    {
        target ??= ScoreTarget.ForChannel(TestData.Track(), "bass");
        Api.GetScoreOptionsAsync(target.Source, Arg.Any<CancellationToken>()).Returns(options);
        return RenderAtLevel<ScoreDialog>(level, p => p.Add(c => c.Target, target));
    }
}
