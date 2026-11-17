using System.Text.Json;
using StemStudio.Web.Practice;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Tests.Practice;

/// <summary>T046: the effective key map, pedal presets, chord display and the schema version 2 record (FR-019, FR-022, FR-023).</summary>
public sealed class KeyMapBuilderTests
{
    private static readonly KeyMapCatalogue Catalogue = KeyMapDefaults.Catalogue;

    private static ShortcutPreferences With(params (string Command, ShortcutChord[] Chords)[] overrides) =>
        ShortcutPreferences.Default with { Overrides = overrides.ToDictionary(o => o.Command, o => (IReadOnlyList<ShortcutChord>)o.Chords) };

    private static KeyBinding Keys(KeyMap map, string command) => map.Bindings.Single(b => b.Command == command && b.Layer == "keys");

    [Fact]
    public void Defaults_GiveEveryRowOfTheScopeAndTheDefaultPreset()
    {
        var map = KeyMapBuilder.Build(Catalogue, ShortcutPreferences.Default, "mixer");

        map.Bindings.Count(b => b.Layer == "keys").ShouldBe(Catalogue.Commands.Count(c => c.Scope == "mixer"));
        map.Bindings.Where(b => b.Layer == "pedal").Select(b => (b.Command, b.Chords.Single().Key))
            .ShouldBe([("mixer.play-pause", "pagedown"), ("mixer.section.restart", "pageup")], ignoreOrder: true);
        (map.SingleKey, map.PedalMode).ShouldBe((true, false));
        Keys(map, "mixer.record.toggle").Reserved.ShouldBeFalse();
        var held = Catalogue with { Commands = [.. Catalogue.Commands.Select(c => c.Command == "mixer.stop" ? c with { Reserved = true } : c)] };
        Keys(KeyMapBuilder.Build(held, ShortcutPreferences.Default, "mixer"), "mixer.stop").Reserved.ShouldBeTrue();
        Keys(KeyMapBuilder.Build(Catalogue, ShortcutPreferences.Default, "global"), "palette.open").Anywhere.ShouldBeTrue();
    }

    [Fact]
    public void Overrides_ReplaceTheKeysLayer_AndAnEmptyListUnbinds()
    {
        var map = KeyMapBuilder.Build(Catalogue, With(("mixer.section.next", [new ShortcutChord("j")]), ("mixer.stop", [])), "mixer");

        Keys(map, "mixer.section.next").Chords.ShouldBe([new KeyChordData("j")]);
        Keys(map, "mixer.stop").Chords.ShouldBeEmpty();
    }

    [Fact]
    public void APedalOnlyOverride_KeepsTheDefaultKeys_AndReplacesThePresetPedal()
    {
        var map = KeyMapBuilder.Build(Catalogue, With(("mixer.play-pause", [new ShortcutChord("b", Layer: KeyLayer.Pedal)])), "mixer");

        Keys(map, "mixer.play-pause").Chords.ShouldBe([new KeyChordData(" ")]);
        map.Bindings.Single(b => b.Command == "mixer.play-pause" && b.Layer == "pedal").Chords.ShouldBe([new KeyChordData("b")]);
    }

    [Fact]
    public void SettingsPassThrough_AndTheSetlistPresetBindsInTheSetlistScope()
    {
        var prefs = ShortcutPreferences.Default with { SingleKey = false, PedalMode = true, PedalPreset = "next-previous-song" };

        var setlist = KeyMapBuilder.Build(Catalogue, prefs, "setlist");

        (setlist.SingleKey, setlist.PedalMode).ShouldBe((false, true));
        setlist.Bindings.Where(b => b.Layer == "pedal").Select(b => b.Command).ShouldBe(["setlist.next", "setlist.previous"], ignoreOrder: true);
        KeyMapBuilder.Build(Catalogue, prefs, "mixer").Bindings.ShouldNotContain(b => b.Layer == "pedal");
    }

    [Fact]
    public void Presets_ChooseLearnAndCustom()
    {
        var prefs = PedalPresets.Learn(Catalogue, ShortcutPreferences.Default, PedalSide.Right, new KeyChordData("b"));
        PedalPresets.KeyOf(Catalogue, prefs, PedalSide.Right).ShouldBe(new KeyChordData("b"));
        PedalPresets.KeyOf(Catalogue, prefs, PedalSide.Left).Key.ShouldBe("pageup");

        var moved = PedalPresets.Choose(Catalogue, prefs, "faster-slower");
        moved.PedalPreset.ShouldBe("faster-slower");
        PedalPresets.PedalChords(moved, "mixer.speed:5").ShouldBe([new ShortcutChord("b", Layer: KeyLayer.Pedal)]);
        moved.Overrides.ShouldNotContainKey("mixer.play-pause");
        moved.Overrides.ShouldNotContainKey("mixer.speed:-5");

        var custom = PedalPresets.Choose(Catalogue, moved, PedalPresets.Custom);
        PedalPresets.CommandOf(Catalogue, custom, PedalSide.Left).ShouldBeNull();
        PedalPresets.PedalChords(custom, "mixer.speed:-5").Single().Key.ShouldBe("pageup");
        PedalPresets.Learn(Catalogue, custom, PedalSide.Left, new KeyChordData("x")).ShouldBeSameAs(custom);
        PedalPresets.Choose(Catalogue, custom, "nope").ShouldBeSameAs(custom);
    }

    [Fact]
    public void Learn_KeepsOneKeysChordOfTheCommand()
    {
        var prefs = With(("mixer.play-pause", [new ShortcutChord("j"), new ShortcutChord("u")]));

        var learned = PedalPresets.Learn(Catalogue, prefs, PedalSide.Right, new KeyChordData("b"));

        learned.Overrides["mixer.play-pause"].ShouldBe([new ShortcutChord("j"), new ShortcutChord("b", Layer: KeyLayer.Pedal)]);
        PedalPresets.Choose(Catalogue, learned, "next-previous-section").Overrides["mixer.play-pause"].ShouldBe([new ShortcutChord("j")]);
    }

    [Fact]
    public void Current_FallsBackToTheDefaultPreset()
    {
        PedalPresets.Current(Catalogue, ShortcutPreferences.Default with { PedalPreset = "gone" }).Id.ShouldBe("play-section-start");
    }

    [Theory]
    [InlineData("s", true, true, "Ctrl+Shift+S")]
    [InlineData(" ", false, null, "Space")]
    [InlineData("pagedown", false, null, "PageDown")]
    [InlineData("arrowleft", true, null, "Ctrl+Left")]
    [InlineData("arrowright", false, true, "Shift+Right")]
    [InlineData("arrowup", false, null, "Up")]
    [InlineData("arrowdown", false, null, "Down")]
    [InlineData("pageup", false, null, "PageUp")]
    [InlineData("backspace", false, null, "Backspace")]
    [InlineData("l", false, false, "L")]
    public void Display_ShowsTheChord(string key, bool mod, bool? shift, string text)
    {
        KeyChords.Display(new KeyChordData(key, mod, shift)).ShouldBe(text);
    }

    [Fact]
    public void Record_SchemaVersion2_RoundTripsTheShortcuts()
    {
        var prefs = UserPreferences.Default with
        {
            Shortcuts = new ShortcutPreferences(false, true, "faster-slower", With(("mixer.stop", [new ShortcutChord("j", true, false)])).Overrides),
        };

        var back = PreferencesJson.FromElement(PreferencesJson.ToElement(prefs)).Shortcuts;

        (back.SingleKey, back.PedalMode, back.PedalPreset).ShouldBe((false, true, "faster-slower"));
        back.Overrides["mixer.stop"].ShouldBe([new ShortcutChord("j", true, false)]);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"shortcuts":{"singleKey":false}}""", true, "play-section-start", 0)]
    [InlineData("""{"schemaVersion":2}""", true, "play-section-start", 0)]
    [InlineData("""{"schemaVersion":2,"shortcuts":{"singleKey":0,"pedalPreset":"jump","overrides":{"mixer.fly":[]}}}""", true, "play-section-start", 0)]
    [InlineData("""{"schemaVersion":2,"shortcuts":{"singleKey":false,"pedalPreset":"custom","overrides":{"mixer.stop":[]}}}""", false, "custom", 1)]
    public void Record_ReadsShortcutsFieldByField(string json, bool singleKey, string preset, int overrides)
    {
        var shortcuts = PreferencesJson.FromElement(JsonDocument.Parse(json).RootElement).Shortcuts;

        (shortcuts.SingleKey, shortcuts.PedalPreset, shortcuts.Overrides.Count).ShouldBe((singleKey, preset, overrides));
    }

    [Fact]
    public void Catalogue_IsEmbedded_AndAMissingResourceIsReported()
    {
        KeyMapDefaults.Catalogue.Commands.Count.ShouldBe(44);
        Should.Throw<FileNotFoundException>(() => KeyMapDefaults.Read(null));
    }

    [Fact]
    public void Arguments_AreChecked()
    {
        Should.Throw<ArgumentNullException>(() => KeyMapBuilder.Build(null!, ShortcutPreferences.Default, "mixer"));
        Should.Throw<ArgumentNullException>(() => KeyMapBuilder.Build(Catalogue, null!, "mixer"));
        Should.Throw<ArgumentNullException>(() => KeyMapBuilder.KeysChords(null!, ShortcutPreferences.Default));
        Should.Throw<ArgumentNullException>(() => KeyMapBuilder.KeysChords(Catalogue.Commands[0], null!));
        Should.Throw<ArgumentNullException>(() => KeyMapBuilder.PedalChords(Catalogue, null!, ShortcutPreferences.Default));
        Should.Throw<ArgumentNullException>(() => PedalPresets.Current(null!, ShortcutPreferences.Default));
        Should.Throw<ArgumentNullException>(() => PedalPresets.Current(Catalogue, null!));
        Should.Throw<ArgumentNullException>(() => PedalPresets.Learn(Catalogue, ShortcutPreferences.Default, PedalSide.Left, null!));
        Should.Throw<ArgumentNullException>(() => PedalPresets.Choose(null!, ShortcutPreferences.Default, "custom"));
        Should.Throw<ArgumentNullException>(() => PedalPresets.PedalChords(null!, "x"));
        Should.Throw<ArgumentNullException>(() => KeyChords.EffectiveShift(null!));
        Should.Throw<ArgumentNullException>(() => KeyChords.Clash(null!, new KeyChordData("j")));
        Should.Throw<ArgumentNullException>(() => KeyChords.Clash(new KeyChordData("j"), null!));
        Should.Throw<ArgumentNullException>(() => KeyChords.Display(null!));
    }
}
