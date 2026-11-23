using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StemStudio.Api.Handouts;
using StemStudio.Api.Insights;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Handouts;
using StemStudio.Domain.Handouts;

namespace StemStudio.Api.Tests.Insights;

/// <summary>Edge cases of the security review 020 helpers: strict mapping, request rules, job cleanup and the start locks.</summary>
public sealed class SecurityHelperTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void TheMapper_NeverConvertsAnUndefinedValue()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => HandoutMapper.Convert<HandoutFamily>(WireEnumConverter<HandoutFamilyDto>.Undefined));
        HandoutMapper.Convert<HandoutFamily>(HandoutFamilyDto.Drums).ShouldBe(HandoutFamily.Drums);
    }

    [Fact]
    public void TheRequestRules_CoverMissingSourceAndTooManyNotationIds()
    {
        var request = new CreateHandoutRequest
        {
            Source = null!,
            Family = HandoutFamilyDto.Bass,
            Include = [IngredientCodeDto.Facts],
            NotationAssetIds = [.. Enumerable.Range(0, HandoutRequests.MaxNotationIds + 1).Select(_ => Guid.NewGuid())],
        };

        var errors = Errors(HandoutRequests.Validate(request)!);

        errors.Keys.ShouldBe(["source", "notationAssetIds"], ignoreOrder: true);
    }

    [Fact]
    public async Task KeepAsync_PassesOtherFailuresOn_AndSurvivesAFailedDelete()
    {
        var deletes = 0;
        Task Delete()
        {
            deletes++;
            throw new WorkerUnavailableException("down");
        }

        await Should.ThrowAsync<InvalidOperationException>(() => SubmittedJob.KeepAsync(() => throw new InvalidOperationException("disk"), Delete, NullLogger.Instance));
        var invalid = await SubmittedJob.KeepAsync(() => throw new ArgumentException("bad id"), () => throw new ArgumentException("bad path"), NullLogger.Instance);
        var kept = await SubmittedJob.KeepAsync(() => Task.FromResult<IResult?>(null), Delete, NullLogger.Instance);

        deletes.ShouldBe(1);
        invalid.ShouldBeAssignableTo<IStatusCodeHttpResult>()!.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        kept.ShouldBeNull();
    }

    [Fact]
    public async Task TheStartLocks_RunOneActionAtATime_AndDispose()
    {
        using var gate = new HandoutGate();
        var running = 0;
        var most = 0;
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => gate.RunAsync(
            async () =>
            {
                most = Math.Max(most, Interlocked.Increment(ref running));
                await Task.Yield();
                Interlocked.Decrement(ref running);
                return true;
            },
            Ct)));

        most.ShouldBe(1);
        new QualityGate().Dispose();
    }

    [Fact]
    public async Task APracticeNote_WithoutABodyOrFamily_Is422()
    {
        using var factory = new ApiFactory();
        factory.CreateClient().Dispose();

        var (noBody, noFamily) = await factory.WithScopeAsync(async sp =>
        {
            var service = sp.GetRequiredService<PracticeNoteService>();
            return (await service.SetAsync(Guid.NewGuid(), "bass", null!, Ct), await service.SetAsync(Guid.NewGuid(), null!, new SetPracticeNoteRequest { Text = "x" }, Ct));
        });

        Errors(noBody).Keys.ShouldBe(["text"]);
        Errors(noFamily).Keys.ShouldBe(["family"]);
    }

    private static IDictionary<string, string[]> Errors(IResult result)
    {
        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe((int)HttpStatusCode.UnprocessableEntity);
        return problem.ProblemDetails.ShouldBeOfType<HttpValidationProblemDetails>().Errors;
    }
}
