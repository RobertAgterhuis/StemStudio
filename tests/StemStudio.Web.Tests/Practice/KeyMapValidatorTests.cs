using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Tests.Practice;

/// <summary>
/// T046: the override rules of <see cref="KeyMapValidator"/> equal the TS rules (<c>src/web-interop/src/overrides.ts</c>): both run the
/// shared cases <c>keymap-override-cases.json</c> on the shared catalogue <c>keymap-defaults.json</c> (FR-019, FR-021, FR-022).
/// </summary>
public sealed class KeyMapValidatorTests
{
    private static readonly KeyMapCatalogue Catalogue = KeyMapDefaults.Catalogue;

    private static readonly Lazy<JsonElement> Cases = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Practice", "keymap-override-cases.json"))).RootElement.GetProperty("cases"));

    public static TheoryData<string> CaseNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var testCase in Cases.Value.EnumerateArray())
            {
                data.Add(testCase.GetProperty("name").GetString()!);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void SharedCase_GivesTheSameResultAsTheBrowser(string name)
    {
        var testCase = Cases.Value.EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);

        var result = KeyMapValidator.Validate(Catalogue, testCase.GetProperty("input"));

        var expected = JsonNode.Parse(testCase.GetProperty("overrides").GetRawText());
        JsonNode.DeepEquals(ShortcutJson.OverridesNode(result.Overrides), expected).ShouldBeTrue(ShortcutJson.OverridesNode(result.Overrides).ToJsonString());
        result.Dropped.ShouldBe([.. testCase.GetProperty("dropped").EnumerateArray().Select(d => d.GetString()!)]);
    }

    [Fact]
    public void Validate_WithoutAValue_GivesNoOverrides()
    {
        var result = KeyMapValidator.Validate(Catalogue, (JsonElement?)null);

        result.Overrides.ShouldBeEmpty();
        result.Dropped.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_TypedOverrides_FollowsTheSameRules()
    {
        var overrides = new Dictionary<string, IReadOnlyList<ShortcutChord>>
        {
            ["mixer.stop"] = [new ShortcutChord("j")],
            ["mixer.loop.a"] = [new ShortcutChord("j"), new ShortcutChord("b", Layer: KeyLayer.Pedal)],
        };

        var result = KeyMapValidator.Validate(Catalogue, overrides);

        result.Overrides["mixer.loop.a"].ShouldBe([new ShortcutChord("b", Layer: KeyLayer.Pedal)]);
        result.Dropped.ShouldBe(["mixer.loop.a: conflicts with mixer.stop"]);
    }

    [Fact]
    public void Validate_KeepsAtMostSixtyCommands()
    {
        var all = Catalogue.Commands.Where(c => !c.Reserved).ToDictionary(c => c.Command, _ => (IReadOnlyList<ShortcutChord>)[]);

        all.Count.ShouldBeLessThan(KeyMapValidator.MaxOverrides);
        KeyMapValidator.Validate(Catalogue, all).Overrides.Keys.ShouldBe(all.Keys);
    }

    [Theory]
    [InlineData("j", false, null, null)]
    [InlineData("l", false, null, "conflicts with mixer.loop.toggle")]
    [InlineData("n", false, null, null)]
    [InlineData("f5", false, null, "Reloads the page")]
    [InlineData("g", false, null, "Starts the \"G then\" sequences")]
    [InlineData("r", false, null, "conflicts with mixer.record.toggle")]
    public void Rejection_NamesWhyAChordCannotBeAssigned(string key, bool mod, bool? shift, string? reason)
    {
        var none = new Dictionary<string, IReadOnlyList<ShortcutChord>>();

        KeyMapValidator.Rejection(Catalogue, none, "mixer.section.next", new KeyChordData(key, mod, shift)).ShouldBe(reason);
    }

    [Fact]
    public void Rejection_UsesTheOverridesOfTheOtherCommands()
    {
        var overrides = new Dictionary<string, IReadOnlyList<ShortcutChord>> { ["mixer.loop.toggle"] = [new ShortcutChord("j")] };

        KeyMapValidator.Rejection(Catalogue, overrides, "mixer.section.next", new KeyChordData("l")).ShouldBeNull();
        KeyMapValidator.Rejection(Catalogue, overrides, "mixer.section.next", new KeyChordData("j")).ShouldBe("conflicts with mixer.loop.toggle");
    }

    [Theory]
    [InlineData("tab", false, true, "Moves the keyboard focus")]
    [InlineData("w", true, null, "Browser shortcut")]
    [InlineData("f8", false, true, "Used elsewhere: Previous suggestion")]
    [InlineData("j", false, null, null)]
    public void ReservedReason_MatchesTheBrowser(string key, bool mod, bool? shift, string? reason)
    {
        KeyMapValidator.ReservedReason(Catalogue, new KeyChordData(key, mod, shift)).ShouldBe(reason);
    }

    [Fact]
    public void Arguments_AreChecked()
    {
        var none = new Dictionary<string, IReadOnlyList<ShortcutChord>>();
        Should.Throw<ArgumentNullException>(() => KeyMapValidator.Validate(null!, (JsonElement?)null));
        Should.Throw<ArgumentNullException>(() => KeyMapValidator.Rejection(null!, none, "x", new KeyChordData("j")));
        Should.Throw<ArgumentNullException>(() => KeyMapValidator.Rejection(Catalogue, null!, "x", new KeyChordData("j")));
        Should.Throw<ArgumentNullException>(() => KeyMapValidator.ReservedReason(null!, new KeyChordData("j")));
        Should.Throw<ArgumentNullException>(() => ShortcutJson.ChordNode(null!));
        Should.Throw<ArgumentNullException>(() => ShortcutJson.OverridesNode(null!));
    }
}
