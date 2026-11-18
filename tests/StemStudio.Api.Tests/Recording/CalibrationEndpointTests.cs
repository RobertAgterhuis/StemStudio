using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Recording;
using StemStudio.Domain.Recording;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Recording;

/// <summary>Feature 015 T047: input calibrations - list, upsert by label (create/replace), validation, delete (takes keep their latency).</summary>
public sealed class CalibrationEndpointTests : RecordingTestBase
{
    private static InputCalibrationInput Loopback(string label = "Focusrite Scarlett 2i2") => new()
    {
        DeviceLabel = label,
        DeviceIdHint = "a1b2",
        LatencySamples = 1102,
        Method = CalibrationMethodDto.Loopback,
        SpreadSamples = 3,
        DetectedClicks = 8,
        OutputLatencySamples = 441,
    };

    private Task<HttpResponseMessage> PutAsync(object input) => Client.PutAsJsonAsync(new Uri(ApiRoutes.Calibrations, UriKind.Relative), input, ApiFactory.Json, Ct);

    [Fact]
    public async Task Upsert_Creates_ThenReplacesTheSameTrimmedLabel()
    {
        using var created = await PutAsync(Loopback());
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(created, "upsertInputCalibration");
        var first = await created.BodyAsync<InputCalibrationDto>();
        (first.DeviceLabel, first.LatencyMilliseconds, first.Method, first.MeasuredAt).ShouldBe(("Focusrite Scarlett 2i2", 24.99, CalibrationMethodDto.Loopback, ApiFactory.Start));

        Factory.Clock.Advance(TimeSpan.FromDays(1));
        using var replaced = await PutAsync(Loopback("  Focusrite Scarlett 2i2 ") with { Method = CalibrationMethodDto.Manual, LatencySamples = 900, SpreadSamples = null, DetectedClicks = null });
        var second = await replaced.BodyAsync<InputCalibrationDto>();

        (second.Id, second.LatencySamples, second.Method, second.SpreadSamples, second.MeasuredAt).ShouldBe((first.Id, 900, CalibrationMethodDto.Manual, (int?)null, ApiFactory.Start.AddDays(1)));
        using var list = await Client.GetAsync(new Uri(ApiRoutes.Calibrations, UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(list, "listInputCalibrations");
        (await list.BodyAsync<List<InputCalibrationDto>>()).ShouldHaveSingleItem().Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task List_IsSortedByLabel()
    {
        (await PutAsync(Loopback("b"))).Dispose();
        (await PutAsync(Loopback("a"))).Dispose();

        var list = await Client.GetFromJsonAsync<List<InputCalibrationDto>>(new Uri(ApiRoutes.Calibrations, UriKind.Relative), ApiFactory.Json, Ct);

        list!.Select(calibration => calibration.DeviceLabel).ShouldBe(["a", "b"]);
    }

    [Theory]
    [InlineData("deviceLabel")]
    [InlineData("latencySamples")]
    [InlineData("spreadSamples")]
    [InlineData("detectedClicks")]
    [InlineData("deviceIdHint")]
    public async Task InvalidValues_Are422(string field)
    {
        var input = field switch
        {
            "deviceLabel" => Loopback("  "),
            "latencySamples" => Loopback() with { LatencySamples = 44_101 },
            "spreadSamples" => Loopback() with { SpreadSamples = 45 },
            "detectedClicks" => Loopback() with { DetectedClicks = 5 },
            _ => Loopback() with { DeviceIdHint = new string('h', 513) },
        };

        using var response = await PutAsync(input);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "upsertInputCalibration");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain(field);
    }

    [Fact]
    public async Task Delete_Is204_TakesKeepTheirLatency_AndUnknownIs404()
    {
        var calibration = await (await PutAsync(Loopback())).BodyAsync<InputCalibrationDto>();
        var track = await TrackAsync();
        var take = await CreateAsync(track.Id, Metadata() with { CalibrationId = calibration.Id, LatencyMethod = LatencyMethodDto.Loopback });
        take.Latency.CalibrationId.ShouldBe(calibration.Id);

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.CalibrationOf(calibration.Id), UriKind.Relative), Ct);
        using var again = await Client.DeleteAsync(new Uri(ApiRoutes.CalibrationOf(calibration.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.MatchesAsync(deleted, "deleteInputCalibration");
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(again, "deleteInputCalibration");
        var stored = (await TakesAsync()).ShouldHaveSingleItem();
        (stored.CalibrationId, stored.LatencySamples, stored.LatencyMethod).ShouldBe(((Guid?)null, 1102, LatencyMethod.Loopback));
    }

    [Fact]
    public async Task UnusedTrack_IsIrrelevant_ToCalibrations()
    {
        Track? none = null;
        none.ShouldBeNull();
        using var response = await Client.GetAsync(new Uri(ApiRoutes.Calibrations, UriKind.Relative), Ct);
        (await response.BodyAsync<List<InputCalibrationDto>>()).ShouldBeEmpty();
    }
}
