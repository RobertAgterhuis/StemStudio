using StemStudio.Api.LibraryCare.Archive;
using StemStudio.Api.Tests.Contract;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>014 T108: backups record the API version, which is the <c>info.version</c> of the current full backend document.</summary>
public sealed class ManifestApiVersionTests
{
    [Fact]
    public void BackendApiVersion_IsTheVersionOfTheCurrentFullDocument()
    {
        var version = OpenApiContract.Backend.Root["info"]!["version"]!.GetValue<string>();

        ManifestBuilder.BackendApiVersion.ShouldBe(version);
        version.ShouldBe("1.15.0");
    }
}
