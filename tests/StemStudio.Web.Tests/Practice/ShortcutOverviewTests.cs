using StemStudio.Web.Practice;

namespace StemStudio.Web.Tests.Practice;

/// <summary>T062: the live overview model (FR-024): groups in catalogue order, reserved rows hidden, sequences, text and data-keys.</summary>
public sealed class ShortcutOverviewTests
{
    private static readonly KeyMapCatalogue Catalogue = KeyMapDefaults.Catalogue;

    [Fact]
    public void ReservedRows_AreHidden()
    {
        var held = Catalogue with { Commands = [.. Catalogue.Commands.Select(c => c.Command == "mixer.stop" ? c with { Reserved = true } : c)] };

        var lines = ShortcutOverview.Build(held, ShortcutPreferences.Default, ["mixer"]).SelectMany(g => g.Lines);

        lines.ShouldNotContain(l => l.Command == "mixer.stop");
        lines.ShouldContain(l => l.Command == "mixer.loop.toggle");
    }

    [Fact]
    public void GlobalScope_HasOnlyTheNavigationGroup_WithTheSequences()
    {
        var groups = ShortcutOverview.Build(Catalogue, ShortcutPreferences.Default, ["global"]);

        groups.Select(g => g.Group).ShouldBe(["navigation"]);
        groups[0].Title.ShouldBe("Everywhere");
        groups[0].Lines.Single(l => l.Command == "go.mixes").Text.ShouldBe("G then M");
        groups[0].Lines.Single(l => l.Command == "help.open").Text.ShouldBe("? or F1");
    }

    [Fact]
    public void Lines_FormatTheirIdsKeysAndText()
    {
        var line = new ShortcutLine("mixer.seek:-5", "Back 5 seconds", [new KeyChordData("arrowleft", false, false)], [new KeyChordData("pageup")]);

        (line.TestId, line.DataKeys, line.Text).ShouldBe(("mixer-seek--5", "arrowleft|pageup", "Left or PageUp (pedal)"));
        ShortcutLine.Wire(new KeyChordData("s", true, true)).ShouldBe("mod+shift+s");
        ShortcutOverview.Title("unknown").ShouldBe("unknown");
        Should.Throw<ArgumentNullException>(() => ShortcutLine.Wire(null!));
        Should.Throw<ArgumentNullException>(() => ShortcutOverview.Build(null!, ShortcutPreferences.Default, ["mixer"]));
        Should.Throw<ArgumentNullException>(() => ShortcutOverview.Build(Catalogue, ShortcutPreferences.Default, null!));
    }
}
