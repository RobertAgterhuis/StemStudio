using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Tests.Contract;

/// <summary>Skeleton tests (architect, feature 003 Phase 0): device DTOs and route against the backend contract (1.3.0 additions; the version is asserted in AssetContractTests).</summary>
public sealed class DeviceContractTests
{
    private const string Gpu = "NVIDIA GeForce RTX 5070 Ti";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SeparatingTrackExample_CarriesTheGpuDeviceAndRoundTrips()
    {
        var json = Example("SeparatingTrack");

        var dto = json.Deserialize<TrackDto>(Web)!;
        var again = JsonSerializer.SerializeToNode(dto, Web);

        dto.Device.ShouldBe(new JobDeviceDto(ProcessingDeviceKindDto.Gpu, Gpu, false, null));
        dto.Detail.Device.ShouldBeNull();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Track"), again).ShouldBeEmpty();
    }

    [Fact]
    public void DetailCompletedExample_CarriesTheDetailFallback()
    {
        var dto = Example("DetailCompletedTrack").Deserialize<TrackDto>(Web)!;

        dto.Detail.Device!.Kind.ShouldBe(ProcessingDeviceKindDto.Gpu);
        dto.Detail.Device.CpuFallback.ShouldBeTrue();
        dto.Detail.Device.FallbackReason.ShouldNotBeNullOrWhiteSpace();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Track"), JsonSerializer.SerializeToNode(dto, Web)).ShouldBeEmpty();
    }

    [Fact]
    public void TracksWithoutDevice_SerializeANullDeviceThatMatchesTheSchema()
    {
        var dto = Example("QueuedTrack").Deserialize<TrackDto>(Web)!;
        var node = JsonSerializer.SerializeToNode(dto, Web)!;

        dto.Device.ShouldBeNull();
        node["device"].ShouldBeNull();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Track"), node).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("gpu", true, ProcessingDeviceKindDto.Gpu)]
    [InlineData("cpuWithReason", true, ProcessingDeviceKindDto.Cpu)]
    [InlineData("unreachable", false, null)]
    public void DeviceExamples_RoundTripThroughProcessingDeviceDto(string example, bool reachable, ProcessingDeviceKindDto? kind)
    {
        var json = DeviceExample(example);

        var dto = json.Deserialize<ProcessingDeviceDto>(Web)!;
        var again = JsonSerializer.SerializeToNode(dto, Web);

        dto.Reachable.ShouldBe(reachable);
        dto.Kind.ShouldBe(kind);
        JsonNode.DeepEquals(again, json).ShouldBeTrue();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("ProcessingDevice"), again).ShouldBeEmpty();
    }

    [Fact]
    public void UnreachableDevice_HasOnlyNullFields()
    {
        var node = JsonSerializer.SerializeToNode(ProcessingDeviceDto.Unreachable, Web)!;

        node["reachable"]!.GetValue<bool>().ShouldBeFalse();
        node["kind"].ShouldBeNull();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("ProcessingDevice"), node).ShouldBeEmpty();
    }

    [Fact]
    public void DeviceKind_MatchesTheContractEnum()
    {
        var contract = Schema("ProcessingDeviceKind")["enum"]!.AsArray().Select(v => v!.GetValue<string>());
        var serialized = Enum.GetValues<ProcessingDeviceKindDto>().Select(k => JsonSerializer.SerializeToNode(k, Web)!.GetValue<string>());

        serialized.ShouldBe(contract);
    }

    [Theory]
    [InlineData(ProcessingDeviceKindDto.Gpu, Gpu, "GPU (NVIDIA GeForce RTX 5070 Ti)")]
    [InlineData(ProcessingDeviceKindDto.Cpu, null, "CPU")]
    public void DisplayText_FollowsTheUiContract(ProcessingDeviceKindDto kind, string? name, string expected)
    {
        new JobDeviceDto(kind, name, false, null).DisplayText().ShouldBe(expected);
    }

    [Fact]
    public void DeviceRoute_IsTheContractPath()
    {
        OpenApiContract.Backend.Root["paths"]![ApiRoutes.Device].ShouldNotBeNull();
    }

    private static JsonNode Schema(string name) => OpenApiContract.Backend.Root["components"]!["schemas"]![name]!;

    private static JsonNode Example(string name) => OpenApiContract.Backend.Root["components"]!["examples"]![name]!["value"]!.DeepClone();

    private static JsonNode DeviceExample(string name) =>
        OpenApiContract.Backend.Root["paths"]![ApiRoutes.Device]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]![name]!["value"]!.DeepClone();
}
