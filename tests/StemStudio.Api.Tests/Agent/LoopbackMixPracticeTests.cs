using StemStudio.Api.Agent.Loopback;
using StemStudio.Api.Tests.Mixes;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.Mixes;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// 012 T063: the composer agent's mix calls go through the loopback client, so <c>practice</c> must survive its JSON round
/// trip; <c>rename_mix</c> and <c>restore_mix</c> write <c>previous.Settings</c> back and must not reset the practice tempo.
/// </summary>
public sealed class LoopbackMixPracticeTests : ApiTestBase
{
    private static readonly PracticeSettingsDto SlowPractice =
        new(80, 3, new SpeedTrainerSettingsDto(true, 65, 3, 95, 4), new CountInSettingsDto(2, false));

    private readonly LoopbackActions _actions;

    public LoopbackMixPracticeTests() =>
        _actions = new LoopbackActions(new LoopbackClient(new HttpClient(Factory.Server.CreateHandler()) { BaseAddress = new Uri("http://127.0.0.1:5000/") }));

    [Fact]
    public async Task CreateGetAndUpdate_KeepThePractice()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var created = await _actions.Mixes.CreateMixAsync(new CreateMixRequest(track.Id, MixEndpointsTests.Settings() with { Practice = SlowPractice }), Ct);
        var previous = await _actions.Mixes.GetMixAsync(created.Id, Ct);
        var renamed = await _actions.Mixes.UpdateMixAsync(created.Id, previous.Settings with { Name = "Renamed" }, Ct);
        var copy = await _actions.Mixes.DuplicateMixAsync(created.Id, Ct);

        (created.Settings.Practice, previous.Settings.Practice).ShouldBe((SlowPractice, SlowPractice));
        (renamed.Settings.Name, renamed.Settings.Practice).ShouldBe(("Renamed", SlowPractice));
        copy.Settings.Practice.ShouldBe(SlowPractice);
    }
}
