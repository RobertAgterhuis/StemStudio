using Microsoft.Extensions.DependencyInjection;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Help.HelpSamples;

namespace StemStudio.Web.Tests.Help;

/// <summary>Page context with a small, known help catalogue (screen topics for "/" and the mixer).</summary>
public abstract class HelpTestContext : PageTestContext
{
    protected static readonly HelpFile[] Content =
    [
        .. Files,
        new("topics/import/import-screen.md", Topic("import-screen", new("Import a song", "Paste a link or drop a file.",
            "route: /\nanchors: [import-url]\nkeywords: [youtube]\nrelated: [stem]", "## Links\n\nPaste a [stem](help:stem) link."), "import")),
        new("topics/tracks/tracks-library.md", Topic("tracks-library", new("Tracks", "Every imported song.", "route: /tracks"), "tracks")),
        new("topics/start/journey.md", Topic("journey", new("Your journey", "Four steps.", "feature: journey.rail"), "start")),
    ];

    protected HelpTestContext()
    {
        Services.AddSingleton<IHelpCatalog>(new HelpCatalog(Content));
        Services.AddScoped<HelpContext>();
    }

    protected HelpContext Help => Services.GetRequiredService<HelpContext>();

    protected void GoTo(string relative) =>
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo(relative);
}
