using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Api.Mixes;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Domain.Mixes;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Mixes;

public sealed class MixServiceTests : IDisposable
{
    private readonly ITrackRepository _tracks = Substitute.For<ITrackRepository>();
    private readonly IMixRepository _mixes = Substitute.For<IMixRepository>();
    private readonly ApiFactory _factory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Create_ConcurrentSameNameIsCaughtByTheUniqueIndex()
    {
        var track = SyncTestData.CompletedTrack();
        _tracks.FindAsync(track.Id, Arg.Any<CancellationToken>()).Returns(track);
        _mixes.AddAsync(Arg.Any<Mix>(), Arg.Any<CancellationToken>()).ThrowsAsync(new DuplicateKeyException());

        var result = await Service().CreateAsync(new CreateMixRequest(track.Id, MixEndpointsTests.Settings()), Ct);

        result.ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task Create_EmptyBodyReturns400OnSettings()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri(ApiRoutes.Mixes, UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("settings")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Update_MissingPartsReturn400WithBodyRelativeKeys()
    {
        var track = SyncTestData.CompletedTrack();
        var mix = Mix.Create(Guid.NewGuid(), track, MixMapper.ToDomain(MixEndpointsTests.Settings()), SyncTestData.T0);
        _mixes.FindAsync(mix.Id, Arg.Any<CancellationToken>()).Returns(mix);

        var result = await Service().UpdateAsync(mix.Id, MixEndpointsTests.Settings() with { Metronome = null! }, Ct);

        result.ShouldBeOfType<ValidationProblem>().ProblemDetails.Errors.ShouldContainKey("metronome");
    }

    [Fact]
    public async Task Update_TrackNotCompletedKeepsTrackIdAtTheRoot()
    {
        var track = SyncTestData.CompletedTrack();
        var mix = Mix.Create(Guid.NewGuid(), track, MixMapper.ToDomain(MixEndpointsTests.Settings()), SyncTestData.T0);
        _mixes.FindAsync(mix.Id, Arg.Any<CancellationToken>()).Returns(mix);
        _tracks.FindAsync(track.Id, Arg.Any<CancellationToken>()).Returns(track);

        var result = await Service().UpdateAsync(mix.Id, MixEndpointsTests.Settings() with { MasterVolume = 3 }, Ct);

        result.ShouldBeOfType<ValidationProblem>().ProblemDetails.Errors.Keys.ShouldBe(["masterVolume"]);
    }

    [Fact]
    public void Problem_PrefixesEverythingButTrackId()
    {
        var result = MixValidator.Problem([new("trackId", "x"), new("loop.end", "y")], "settings");

        result.ShouldBeOfType<ValidationProblem>().ProblemDetails.Errors.Keys.ShouldBe(["trackId", "settings.loop.end"], ignoreOrder: true);
    }

    [Fact]
    public void ToDomain_ReportsNullChannelEntries()
    {
        var (settings, errors) = MixValidator.ToDomain(MixEndpointsTests.Settings() with { Channels = [null!, new ChannelSettingsDto(null!, 1, 0, false, false)] });

        settings.ShouldBeNull();
        errors.Select(e => e.Field).ShouldBe(["channels[0]", "channels[1].stem"]);
    }

    [Fact]
    public void Mapper_MapsLooplessSettingsBothWays()
    {
        var dto = MixEndpointsTests.Settings() with { Loop = null };

        var domain = MixMapper.ToDomain(dto);

        domain.Loop.ShouldBeNull();
        MixMapper.ToDto(domain).Loop.ShouldBeNull();
        domain.Channels.Select(c => c.Stem).ShouldBe([StemNames.Guitar, StemNames.Drums]);
        domain.Metronome.BeatsPerBar.ShouldBe(3);
    }

    private MixService Service() => new(_tracks, _mixes, new FakeTimeProvider(ApiFactory.Start), new MixReferences(Substitute.For<ISectionRepository>(), Substitute.For<IRecordingTakeRepository>()));
}
