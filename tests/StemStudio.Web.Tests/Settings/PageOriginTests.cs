using StemStudio.Web.Agent;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Settings;

/// <summary>The host-name guard of the Claude key controls (011 follow-up claude-key US6, CK-FR-012) [sec].</summary>
public sealed class PageOriginTests
{
    [Theory]
    [InlineData("http://localhost:8080/", true)]
    [InlineData("http://LOCALHOST:8080/", true)]
    [InlineData("http://127.0.0.1:8080/", true)]
    [InlineData("http://[::1]:8080/", true)]
    [InlineData("https://localhost/", true)]
    [InlineData("http://192.168.1.20:8080/", false)]
    [InlineData("http://10.0.0.5/", false)]
    [InlineData("http://127.0.0.2:8080/", false)]
    [InlineData("http://[::2]:8080/", false)]
    [InlineData("http://web:8080/", false)]
    [InlineData("http://stemstudio.local:8080/", false)]
    [InlineData("http://localhost.example.com/", false)]
    public void OnlyALoopbackHostName_IsLocal(string baseUri, bool local)
    {
        var origin = new PageOrigin(new TestNavigationManager(baseUri));

        origin.IsLocal.ShouldBe(local);
        origin.Host.ShouldBe(new Uri(baseUri).Host);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("localhost:8080")]
    public void SomethingElse_IsNotALoopbackHostName(string? host) => PageOrigin.IsLoopbackHost(host).ShouldBeFalse();

    [Fact]
    public void TheHostOfTheFirstRead_IsKept()
    {
        var navigation = new TestNavigationManager("http://localhost:8080/");
        var origin = new PageOrigin(navigation);
        origin.IsLocal.ShouldBeTrue();

        navigation.NavigateTo("http://192.168.1.20:8080/settings");

        origin.IsLocal.ShouldBeTrue();
        origin.Host.ShouldBe("localhost");
    }
}
