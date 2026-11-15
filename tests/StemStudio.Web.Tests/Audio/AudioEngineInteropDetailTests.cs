using System.Text.Json;
using Bunit;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Audio;

/// <summary>Engine contract 2.0.0 additions: detail groups in <c>loadTrack</c> and <c>setExpanded</c>.</summary>
public sealed partial class AudioEngineInteropTests
{
    [Fact]
    public async Task LoadTrack_SendsDetailGroupsOfSplitParents_WithAbsoluteUrlsInDisplayOrder()
    {
        _engine.Setup<double>("loadTrack", _ => true).SetResult(213.04);
        await _interop.InitializeAsync();

        await _interop.LoadTrackAsync(TestData.DetailTrack());

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(_engine.VerifyInvoke("loadTrack").Arguments.Single(), JsonSerializerOptions.Web));
        var groups = json.RootElement.GetProperty("groups");
        groups.EnumerateArray().Select(g => g.GetProperty("parent").GetString()).ShouldBe(["vocals", "drums"]);
        var drums = groups[1].GetProperty("stems");
        drums.EnumerateArray().Select(s => s.GetProperty("name").GetString()).ShouldBe(["kick", "snare", "rest"]);
        drums[1].GetProperty("url").GetString().ShouldBe($"http://public.test:8081/api/tracks/{TestData.TrackId}/detail-stems/drums/snare");
    }

    [Fact]
    public async Task LoadTrack_WithoutDetailStems_SendsNoGroups()
    {
        _engine.Setup<double>("loadTrack", _ => true).SetResult(213.04);
        await _interop.InitializeAsync();

        await _interop.LoadTrackAsync(TestData.Track());

        var json = JsonSerializer.Serialize(_engine.VerifyInvoke("loadTrack").Arguments.Single(), JsonSerializerOptions.Web);
        json.ShouldContain("\"groups\":[]");
    }

    [Fact]
    public async Task SetExpanded_InvokesContractMethod_WithParentAndFlag()
    {
        await _interop.InitializeAsync();

        await _interop.SetExpandedAsync("drums", true);
        await _interop.SetChannelAsync(new("drums.snare", 0.5, 0, true, false));

        _engine.VerifyInvoke("setExpanded").Arguments.ShouldBe(["drums", true]);
        _engine.VerifyInvoke("setChannel").Arguments[0].ShouldBe("drums.snare");
    }
}
