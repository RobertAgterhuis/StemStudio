using Bunit;
using Microsoft.AspNetCore.Components;
using Radzen;
using StemStudio.Web.Components.Layout;

namespace StemStudio.Web.Tests.Layout;

public sealed class MainLayoutTests : BunitContext
{
    public MainLayoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddRadzenComponents();
    }

    [Fact]
    public void Layout_RendersNavLinks_ForImportTracksAndMixes()
    {
        var layout = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddContent(0, "page body"))));

        layout.Find("[data-testid=nav-import]").GetAttribute("href").ShouldBe("");
        layout.Find("[data-testid=nav-tracks]").GetAttribute("href").ShouldBe("tracks");
        layout.Find("[data-testid=nav-mixes]").GetAttribute("href").ShouldBe("mixes");
        layout.Markup.ShouldContain("page body");
        layout.Markup.ShouldContain("StemStudio");
    }

    [Fact]
    public void NavMenu_HasAccessibleLabel()
    {
        var menu = Render<NavMenu>();

        menu.Find("nav").GetAttribute("aria-label").ShouldBe("Main navigation");
        menu.FindComponents<Microsoft.AspNetCore.Components.Routing.NavLink>().Count.ShouldBe(3);
    }
}
