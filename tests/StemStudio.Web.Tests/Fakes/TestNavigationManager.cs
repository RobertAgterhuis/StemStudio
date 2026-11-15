using Microsoft.AspNetCore.Components;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>An initialised <see cref="NavigationManager"/> for service tests outside a renderer.</summary>
public sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/");

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        Uri = ToAbsoluteUri(uri).ToString();
        NotifyLocationChanged(isInterceptedLink: false);
    }
}
