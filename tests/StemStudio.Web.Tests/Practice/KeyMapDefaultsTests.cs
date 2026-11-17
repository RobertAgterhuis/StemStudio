using Microsoft.Extensions.FileProviders;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Tests.Practice;

/// <summary>
/// Skeleton tests (architect, 014 Phase 0 part 2, T006): <c>keymap-defaults.json</c> holds every FR-020 row with its owner and no
/// chord twice; reserved, external and sequence keys are never taken by a default.
/// </summary>
public sealed class KeyMapDefaultsTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Practice");

    private static readonly Lazy<KeyMapCatalogue> Catalogue = new(() => KeyMapDefaults.Parse(File.ReadAllText(Path.Combine(Folder, "keymap-defaults.json"))));

    // Spec FR-020 (CTO decisions 2026-11-16) with 012's shipped command ids (DELTA R-1) and 022's `/` (library scope).
    public static TheoryData<string, string, string, string> Fr020 => new()
    {
        { "mixer.play-pause", "mixer", "001", "space" }, { "mixer.stop", "mixer", "004", "home" },
        { "mixer.seek:-5", "mixer", "004", "arrowleft" }, { "mixer.seek:5", "mixer", "004", "arrowright" },
        { "mixer.seek:-1", "mixer", "004", "shift+arrowleft" }, { "mixer.seek:1", "mixer", "004", "shift+arrowright" },
        { "mixer.loop.a", "mixer", "004", "[" }, { "mixer.loop.b", "mixer", "004", "]" }, { "mixer.loop.toggle", "mixer", "004", "l" },
        { "mixer.metronome.toggle", "mixer", "004", "k" }, { "mixer.channel.select:1", "mixer", "004", "1" },
        { "mixer.channel.select:9", "mixer", "004", "9" }, { "mixer.channel.mute", "mixer", "004", "m" },
        { "mixer.channel.solo", "mixer", "004", "s" }, { "mixer.save", "mixer", "004", "mod+s" }, { "mixer.save-as", "mixer", "004", "mod+shift+s" },
        { "palette.open", "global", "004", "mod+k" }, { "shortcuts.open", "global", "004", "mod+/" }, { "help.open", "global", "004", "?|f1" },
        { "mixer.speed:-5", "mixer", "012", "-" }, { "mixer.speed:5", "mixer", "012", "=|+" }, { "mixer.transpose:-1", "mixer", "012", "," },
        { "mixer.transpose:1", "mixer", "012", "." }, { "mixer.practice.reset", "mixer", "012", "0" },
        { "mixer.countin.toggle", "mixer", "012", "shift+k" }, { "mixer.trainer.toggle", "mixer", "012", "t" },
        { "mixer.loop.section", "mixer", "014", "shift+l" }, { "mixer.section.next", "mixer", "014", "n" },
        { "mixer.section.previous", "mixer", "014", "shift+n" }, { "mixer.section.restart", "mixer", "014", "backspace" },
        { "mixer.score.toggle", "mixer", "014", "f" }, { "mixer.score.follow", "mixer", "014", "shift+f" },
        { "setlist.next", "setlist", "014", "mod+arrowright" }, { "setlist.previous", "setlist", "014", "mod+arrowleft" },
        { "mixer.record.toggle", "mixer", "015", "r" }, { "mixer.compare.cycle", "mixer", "015", "c" },
        { "library.search.focus", "library", "022", "/" },
    };

    [Theory]
    [MemberData(nameof(Fr020))]
    public void EveryFr020Row_IsPresentWithItsOwnerAndDefaults(string command, string scope, string owner, string keys)
    {
        var entry = Catalogue.Value.Commands.Single(c => c.Command == command);

        (entry.Scope, entry.Owner).ShouldBe((scope, owner));
        string.Join('|', entry.Defaults.Select(Display)).ShouldBe(keys);
        entry.Group.ShouldBeOneOf([.. Catalogue.Value.Groups]);
        Catalogue.Value.Scopes.ShouldContain(entry.Scope);
    }

    [Fact]
    public void Catalogue_HasExactlyTheFr020CommandsAndTheGoSequences()
    {
        Catalogue.Value.Commands.Count.ShouldBe(44);
        Catalogue.Value.Commands.Select(c => c.Command).ShouldBeUnique();
        Catalogue.Value.Sequences.Select(s => string.Join(' ', s.Keys)).ShouldBe(["g i", "g t", "g m"]);
        Catalogue.Value.Sequences.Select(s => s.Command).ShouldBe(["go.import", "go.tracks", "go.mixes"]);
        Catalogue.Value.Version.ShouldBe("1.1.0");
    }

    [Fact]
    public void NoChord_IsUsedTwice()
    {
        var chords = Catalogue.Value.Commands.SelectMany(c => c.Defaults).ToList();

        for (var i = 0; i < chords.Count; i++)
        {
            chords.Skip(i + 1).Where(other => Clash(chords[i], other)).ShouldBeEmpty(Display(chords[i]));
        }
    }

    [Fact]
    public void NoDefault_TakesAReservedExternalOrSequenceKey()
    {
        var blocked = Catalogue.Value.ReservedChords.Select(r => new KeyChordData(r.Key, r.Mod, r.Shift))
            .Concat(Catalogue.Value.ExternalChords.Select(e => e.Chord).OfType<KeyChordData>())
            .Append(new KeyChordData("g"))
            .Append(Catalogue.Value.Pedal.LeftKey).Append(Catalogue.Value.Pedal.RightKey).ToList();

        Catalogue.Value.Commands.SelectMany(c => c.Defaults).Where(d => blocked.Exists(b => Clash(b, d))).ShouldBeEmpty();
    }

    [Fact]
    public void OnlyTheFifteenCommandsAreReservedAndOnlyThePaletteFiresAnywhere()
    {
        Catalogue.Value.Commands.Where(c => c.Reserved).Select(c => c.Command).ShouldBe(["mixer.record.toggle", "mixer.compare.cycle"]);
        Catalogue.Value.Commands.Where(c => c.Anywhere).ShouldAllBe(c => c.Defaults.All(d => d.Mod));
        Catalogue.Value.Commands.Single(c => c.Anywhere).Command.ShouldBe("palette.open");
    }

    [Fact]
    public void PedalPresets_UseKnownCommands()
    {
        var pedal = Catalogue.Value.Pedal;
        var known = Catalogue.Value.Commands.Where(c => !c.Reserved).Select(c => c.Command).ToHashSet();

        pedal.Presets.Select(p => p.Id).ShouldBe(["play-section-start", "next-previous-section", "next-previous-song", "faster-slower", "custom"]);
        pedal.Presets.Where(p => p.Id != "custom").ShouldAllBe(p => known.Contains(p.Left!) && known.Contains(p.Right!));
        (pedal.DefaultPreset, pedal.LeftKey.Key, pedal.RightKey.Key).ShouldBe(("play-section-start", "pageup", "pagedown"));
        pedal.Presets[0].Right.ShouldBe("mixer.play-pause");
    }

    [Fact]
    public void ExternalChords_KeepThe023LyricsEditorKeys()
    {
        Catalogue.Value.ExternalChords.Select(e => e.Keys).ShouldBe(["Ctrl+Alt+C", "F8", "Shift+F8", "Alt+Enter", "Alt+Backspace", "Alt+I"]);
        Catalogue.Value.ExternalChords.ShouldAllBe(e => e.Owner == "023");
    }

    [Fact]
    public void Load_ReadsTheCatalogueFromTheWebRoot()
    {
        using var root = new PhysicalFileProvider(Path.Combine(Folder, "webroot"));

        KeyMapDefaults.Load(root).Commands.Count.ShouldBe(44);
    }

    [Fact]
    public void Load_RefusesAMissingCatalogue()
    {
        using var root = new PhysicalFileProvider(Folder);

        Should.Throw<FileNotFoundException>(() => KeyMapDefaults.Load(root)).FileName.ShouldBe(KeyMapDefaults.WebRootPath);
        Should.Throw<ArgumentNullException>(() => KeyMapDefaults.Load(null!));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public void Parse_RefusesInvalidDocuments(string json)
    {
        Should.Throw<InvalidDataException>(() => KeyMapDefaults.Parse(json));
    }

    private static bool Clash(KeyChordData a, KeyChordData b) =>
        a.Key == b.Key && a.Mod == b.Mod && (a.Shift is null || b.Shift is null || a.Shift == b.Shift);

    private static string Display(KeyChordData chord) =>
        (chord.Mod ? "mod+" : string.Empty) + (chord.Shift == true ? "shift+" : string.Empty) + (chord.Key == " " ? "space" : chord.Key);
}
