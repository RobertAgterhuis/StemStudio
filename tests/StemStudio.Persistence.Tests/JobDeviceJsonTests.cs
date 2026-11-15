using System.Text.Json;
using StemStudio.Domain;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Configurations;

namespace StemStudio.Persistence.Tests;

public sealed class JobDeviceJsonTests
{
    private static readonly JobDevice Device = new(ProcessingDeviceKind.Gpu, "NVIDIA GeForce RTX 5070 Ti", true, "ran on the CPU");

    [Fact]
    public void Serialize_RoundTripsWithTheKindByName()
    {
        var json = JobDeviceJson.Serialize(Device);

        json.ShouldContain("\"Kind\":\"Gpu\"");
        JobDeviceJson.Deserialize(json).ShouldBe(Device);
    }

    [Fact]
    public void Deserialize_RejectsNullDocuments()
    {
        Should.Throw<JsonException>(() => JobDeviceJson.Deserialize("null"));
    }

    [Fact]
    public void Deserialize_ChecksTheDomainRulesAgain()
    {
        Should.Throw<DomainValidationException>(() => JobDeviceJson.Deserialize("""{"Kind":"Cpu","Name":"x","CpuFallback":false,"FallbackReason":null}"""));
    }
}
