using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Generation;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Generation;

/// <summary>Generation rows and take actions (009 T043, FR-006, FR-007, FR-008; ui-test-ids 1.8.0).</summary>
public sealed class GenerationListTests : GenerationTestContext
{
    private static readonly Guid Id = GenerationData.GenerationId;

    [Fact]
    public void RunningGeneration_ShowsStatusProgressAndStage_AndCanBeCancelled()
    {
        var page = RenderList(GenerationData.Generation(GenerationStateDto.Running));

        var row = page.Find($"[data-testid=generation-row-{Id}]");
        (row.GetAttribute("data-status"), row.GetAttribute("data-kind")).ShouldBe(("running", "restyle"));
        row.TextContent.ShouldContain("Jazz trio");
        row.TextContent.ShouldContain("Never Gonna Give You Up");
        var progress = page.Find($"[data-testid=generation-progress-{Id}]");
        (progress.GetAttribute("role"), progress.GetAttribute("aria-valuenow")).ShouldBe(("progressbar", "42"));
        page.Find($"[data-testid=generation-stage-{Id}]").TextContent.ShouldBe("Generating take 1 of 1");
        page.FindAll($"[data-testid=generation-cancel-{Id}]").Count.ShouldBe(1);
        page.FindAll($"[data-testid=generation-retry-{Id}]").ShouldBeEmpty();
        page.FindAll($"[data-testid=generation-delete-{Id}]").ShouldBeEmpty();
    }

    [Fact]
    public void WaitingForTheGpu_ShowsTheStateAsText()
    {
        var page = RenderList(GenerationData.Generation(GenerationStateDto.WaitingForGpu) with { Stage = null });

        page.Find($"[data-testid=generation-row-{Id}]").GetAttribute("data-status").ShouldBe("waitingForGpu");
        page.Find($"[data-testid=generation-stage-{Id}]").TextContent.ShouldBe("Waiting for the GPU");
    }

    [Fact]
    public async Task Cancel_ReplacesTheRow()
    {
        Api.CancelGenerationAsync(Id, Arg.Any<CancellationToken>()).Returns(GenerationData.Generation(GenerationStateDto.Cancelled));
        var page = RenderList(GenerationData.Generation(GenerationStateDto.Running));

        await page.Find($"[data-testid=generation-cancel-{Id}]").ClickAsync(new());

        page.Find($"[data-testid=generation-row-{Id}]").GetAttribute("data-status").ShouldBe("cancelled");
        page.FindAll($"[data-testid=generation-retry-{Id}]").Count.ShouldBe(1);
    }

    [Fact]
    public async Task FailedGeneration_ShowsTheError_AndRetryAddsTheNewGeneration()
    {
        var retried = GenerationData.Generation(GenerationStateDto.Queued, GenerationData.OtherGenerationId);
        Api.RetryGenerationAsync(Id, Arg.Any<CancellationToken>()).Returns(retried);
        var page = RenderList(GenerationData.Generation(GenerationStateDto.Failed));

        var error = page.Find($"[data-testid=generation-error-{Id}]");
        (error.GetAttribute("role"), error.TextContent).ShouldBe(("alert", "The GPU ran out of memory, also with the low-memory profile."));
        await page.Find($"[data-testid=generation-retry-{Id}]").ClickAsync(new());

        page.FindAll("[data-testid^=generation-row-]").Select(r => r.GetAttribute("data-testid"))
            .ShouldBe([$"generation-row-{GenerationData.OtherGenerationId}", $"generation-row-{Id}"]);
    }

    [Fact]
    public async Task Delete_AsksForConfirmation_ThenRemovesTheRow()
    {
        var page = RenderList(GenerationData.Generation());

        await page.Find($"[data-testid=generation-delete-{Id}]").ClickAsync(new());
        var confirm = page.Find("[data-testid=generation-delete-confirm]");
        page.Find("[role=dialog]").TextContent.ShouldContain("imported tracks are kept");
        await confirm.ClickAsync(new());

        await Api.Received(1).DeleteGenerationAsync(Id, Arg.Any<CancellationToken>());
        page.FindAll($"[data-testid=generation-row-{Id}]").ShouldBeEmpty();
        page.FindAll("[data-testid=generation-delete-confirm]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_CanBeCancelled()
    {
        var page = RenderList(GenerationData.Generation());

        await page.Find($"[data-testid=generation-delete-{Id}]").ClickAsync(new());
        await page.Find("[data-testid=generation-delete-cancel]").ClickAsync(new());

        await Api.DidNotReceive().DeleteGenerationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        page.FindAll($"[data-testid=generation-row-{Id}]").Count.ShouldBe(1);
    }

    [Fact]
    public async Task ActionFailure_IsShown()
    {
        Api.CancelGenerationAsync(Id, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "Generator unavailable", "The generator service could not be reached."));
        var page = RenderList(GenerationData.Generation(GenerationStateDto.Running));

        await page.Find($"[data-testid=generation-cancel-{Id}]").ClickAsync(new());

        page.Find("[data-testid=generation-action-error]").TextContent.ShouldBe("The generator service could not be reached.");
    }

    [Fact]
    public void ActiveGenerations_ArePolled_UntilTheyAreDone()
    {
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>())
            .Returns([GenerationData.Generation(GenerationStateDto.Running)], [GenerationData.Generation()]);
        var page = Render<StemStudio.Web.Components.Generation.GenerationList>(p => p.Add(c => c.Query, new GenerationQuery(null, TestData.TrackId)));

        Timer.Tick();

        page.WaitForAssertion(() => page.Find($"[data-testid=generation-row-{Id}]").GetAttribute("data-status").ShouldBe("completed"));
        Api.Received(2).ListGenerationsAsync(new GenerationQuery(null, TestData.TrackId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LoadFailure_AndEmptyList_AreShown()
    {
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));
        var failed = Render<StemStudio.Web.Components.Generation.GenerationList>(p => p.Add(c => c.Query, new GenerationQuery()));
        failed.Find("[data-testid=generations-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");

        var empty = RenderList();
        empty.Find("[data-testid=generations-empty]").ShouldNotBeNull();
    }

    [Fact]
    public void CompletedTake_PlaysInAPlainAudioElement_WithAnAiBadge()
    {
        var page = RenderList(GenerationData.Generation());

        var player = page.Find($"[data-testid=take-play-{Id}-1]");
        player.TagName.ShouldBe("AUDIO");
        player.HasAttribute("controls").ShouldBeTrue();
        player.GetAttribute("src").ShouldBe($"http://localhost:8081/api/generations/{Id}/takes/1/audio");
        var badge = page.Find($"[data-testid=take-ai-badge-{Id}-1]");
        (badge.TextContent.Trim(), badge.GetAttribute("data-help-id")).ShouldBe(("AI-generated", "generation-ai-label"));
    }

    [Fact]
    public void TakeDetails_ListStyleStrengthSeedSourceRangeModelAndDevice()
    {
        var page = RenderList(GenerationData.Generation());

        var details = page.Find($"[data-testid=take-details-{Id}-1]");
        details.TagName.ShouldBe("DETAILS");
        var text = details.TextContent;
        foreach (var expected in new[] { "Jazz trio", "Balanced", "4243", "Never Gonna Give You Up", "0:30 to 1:15", "drums, bass, guitar", "acestep-v15-turbo", "8f2c1d0", "GPU (NVIDIA GeForce RTX 4070 Ti SUPER)" })
        {
            text.ShouldContain(expected);
        }

        details.QuerySelector("[data-testid=help-hint-generation-ai-label]").ShouldNotBeNull();
    }
}
