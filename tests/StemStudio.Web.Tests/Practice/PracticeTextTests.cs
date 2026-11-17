using StemStudio.Contracts.Sections;
using StemStudio.Web.Practice;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Practice;

/// <summary>Display texts of sections and the shortcut editor's edge cases (T058, T064).</summary>
public sealed class PracticeTextTests
{
    [Fact]
    public void SectionText_KindsRangesAndStatuses()
    {
        var sections = PracticeFlowData.Sections();

        SectionText.Kind(SectionKindDto.PreChorus).ShouldBe("pre-chorus");
        SectionText.WireKind(SectionKindDto.PreChorus).ShouldBe("pre-chorus");
        SectionText.Range(sections, sections[^1]).ShouldBe("0:56 to 1:04");
        SectionText.Range(sections, sections[0]).ShouldBe("bars 1 to 4");
        SectionText.Status(null).ShouldBe(SectionText.Detecting);
        SectionText.Status(PracticeFlowData.SectionSet(SectionSetStatusDto.Failed) with { Error = null }).ShouldBe(SectionText.Failed);
        SectionText.Status(PracticeFlowData.SectionSet() with { Origin = SectionSetOriginDto.Edited }).ShouldBeNull();
        SectionText.Status(PracticeFlowData.SectionSet() with
        {
            Warnings = [new SectionWarningDto { Code = SectionWarningCodeDto.NoGrid, Message = "No grid" }],
        }).ShouldBe(SectionText.GridUncertain);
        Should.Throw<ArgumentNullException>(() => SectionText.Range(null!, sections[0]));
        Should.Throw<ArgumentNullException>(() => SectionText.Range(sections, null!));
    }

    [Fact]
    public void ShortcutEditor_KeepsThePedalChord_AndChecksArguments()
    {
        var catalogue = KeyMapDefaults.Catalogue;
        var learned = PedalPresets.Learn(catalogue, ShortcutPreferences.Default, PedalSide.Right, new KeyChordData("b"));

        var edit = ShortcutEditor.Assign(catalogue, learned, "mixer.play-pause", new KeyChordData("p", false, false));

        edit.Error.ShouldBeNull();
        edit.Preferences.Overrides["mixer.play-pause"].ShouldBe([new ShortcutChord("p", false, false), new ShortcutChord("b", Layer: KeyLayer.Pedal)]);
        ShortcutEditor.Label(catalogue, "mixer.unknown").ShouldBe("mixer.unknown");
        Should.Throw<ArgumentNullException>(() => ShortcutEditor.Assign(null!, learned, "x", new KeyChordData("j")));
        Should.Throw<ArgumentNullException>(() => ShortcutEditor.Assign(catalogue, null!, "x", new KeyChordData("j")));
        Should.Throw<ArgumentNullException>(() => ShortcutEditor.Assign(catalogue, learned, "x", null!));
        Should.Throw<ArgumentNullException>(() => ShortcutEditor.ResetAll(null!));
        Should.Throw<ArgumentNullException>(() => ShortcutEditor.Clear(null!, "x"));
        Should.Throw<ArgumentNullException>(() => ShortcutEditor.Label(null!, "x"));
    }
}
