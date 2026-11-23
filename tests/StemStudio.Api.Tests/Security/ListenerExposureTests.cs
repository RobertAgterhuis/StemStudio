using Microsoft.Extensions.Logging;
using StemStudio.Api.Security;
using StemStudio.Api.Tests.TestSupport;

namespace StemStudio.Api.Tests.Security;

/// <summary>Refactor 003 step 5: a start-up warning when the API listens on a non-loopback address outside a container.</summary>
public sealed class ListenerExposureTests
{
    [Theory]
    [InlineData("http://localhost:8081", true)]
    [InlineData("http://LOCALHOST:8081", true)]
    [InlineData("http://127.0.0.1:8081", true)]
    [InlineData("http://127.0.0.2:8081", true)]
    [InlineData("http://[::1]:8081", true)]
    [InlineData("https://localhost:7081", true)]
    [InlineData("http://0.0.0.0:8081", false)]
    [InlineData("http://[::]:8081", false)]
    [InlineData("http://*:8081", false)]
    [InlineData("http://+:8081", false)]
    [InlineData("http://192.168.1.10:8081", false)]
    [InlineData("http://stemstudio.lan:8081", false)]
    [InlineData("http://localhost.evil.example:8081", false)]
    [InlineData("not a url", false)]
    public void Address_IsLoopbackOnlyForLoopbackHosts(string address, bool expected) =>
        ListenerExposure.IsLoopback(address).ShouldBe(expected);

    [Fact]
    public void NonLoopbackListener_OutsideAContainer_IsLoggedOnceAsAWarning()
    {
        using var logs = new ListLoggerProvider();
        var logger = logs.CreateLogger("StemStudio.Api.Security.ListenerExposure");

        ListenerExposure.LogStartupWarning(logger, ["http://localhost:8081", "http://[::]:8081", "http://0.0.0.0:8082"], inContainer: false);

        var entry = logs.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("http://[::]:8081, http://0.0.0.0:8082");
        entry.Message.ShouldNotContain("http://localhost");
    }

    [Theory]
    [InlineData(true, "http://[::]:8081")]
    [InlineData(false, "http://localhost:8081")]
    [InlineData(false, null)]
    public void InAContainerOrOnLoopbackOnly_NothingIsLogged(bool inContainer, string? address)
    {
        using var logs = new ListLoggerProvider();
        string[] addresses = address is null ? [] : [address];

        ListenerExposure.LogStartupWarning(logs.CreateLogger("x"), addresses, inContainer);

        logs.Entries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void Container_IsRecognisedByTheDotnetImageVariable(string? value, bool expected) =>
        ListenerExposure.IsContainer(value).ShouldBe(expected);
}
