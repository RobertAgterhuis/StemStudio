using System.Net;
using StemStudio.Api.Generator;

namespace StemStudio.Api.Tests.Generator;

/// <summary>009 T076: the standard constructors of the generator exceptions.</summary>
public sealed class GeneratorExceptionTests
{
    private static readonly InvalidOperationException Cause = new("cause");

    [Fact]
    public void Unavailable_HasTheStandardConstructors()
    {
        new GeneratorUnavailableException().Message.ShouldBe("The generator service is unavailable.");
        new GeneratorUnavailableException("m", Cause).InnerException.ShouldBe(Cause);
    }

    [Fact]
    public void NotInstalledAndIntegrity_HaveTheStandardConstructors()
    {
        new GeneratorNotInstalledException("m").Message.ShouldBe("m");
        new GeneratorNotInstalledException("m", Cause).InnerException.ShouldBe(Cause);
        new GeneratorIntegrityException().Message.ShouldContain("SHA-256");
        new GeneratorIntegrityException("m", Cause).InnerException.ShouldBe(Cause);
    }

    [Fact]
    public void Request_HasTheStandardConstructors()
    {
        (new GeneratorRequestException().StatusCode, new GeneratorRequestException().Code).ShouldBe((HttpStatusCode.BadRequest, null));
        new GeneratorRequestException("d").Detail.ShouldBe("d");
        var wrapped = new GeneratorRequestException("d", Cause);
        (wrapped.Detail, wrapped.InnerException, wrapped.StatusCode).ShouldBe(("d", (Exception)Cause, HttpStatusCode.BadRequest));
    }
}
