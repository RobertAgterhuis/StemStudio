using StemStudio.Domain.Styles;

namespace StemStudio.Domain.Tests.Styles;

public sealed class PresetCatalogTests
{
    private static readonly PresetCatalog Catalog = PresetCatalog.Load();

    [Fact]
    public void Load_HasAtLeast24PresetsIn8Families()
    {
        Catalog.Version.ShouldBe(1);
        Catalog.Presets.Count.ShouldBeGreaterThanOrEqualTo(24);
        Catalog.Presets.Select(p => p.Fields.GenreFamily).Distinct().Count().ShouldBeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void Load_KeysIdsAndNamesAreUnique()
    {
        Catalog.Presets.Select(p => p.Key).Distinct().Count().ShouldBe(Catalog.Presets.Count);
        Catalog.Presets.Select(p => p.Id).Distinct().Count().ShouldBe(Catalog.Presets.Count);
        Catalog.Presets.Select(p => p.Fields.Name.ToUpperInvariant()).Distinct().Count().ShouldBe(Catalog.Presets.Count);
    }

    [Fact]
    public void Load_EveryPresetPassesTheRulesAndTheGuard()
    {
        var guard = StyleTextGuard.Create([]);

        foreach (var preset in Catalog.Presets)
        {
            StyleRules.Validate(preset.Fields).ShouldBeEmpty(preset.Key);
            guard.Check(preset.Fields).ShouldBeEmpty(preset.Key);
            StylePromptBuilder.Caption(Style.FromPreset(preset, StyleTestData.T0).Snapshot()).Length.ShouldBeLessThanOrEqualTo(512);
        }
    }

    [Fact]
    public void Load_JazzTrioMatchesTheContractExample()
    {
        var preset = Catalog.Presets.Single(p => p.Key == "jazz-trio");

        preset.Id.ShouldBe(Guid.Parse("3f9c2a1e-6b4d-4e8f-a0c2-1d3e5f7a9b01"));
        preset.Fields.ShouldBe(StyleTestData.JazzTrio());
    }

    [Fact]
    public void Load_ResearchDefaults()
    {
        Strength Of(string key) => Catalog.Presets.Single(p => p.Key == key).Fields.DefaultStrength;
        VocalHandling Vocals(string key) => Catalog.Presets.Single(p => p.Key == key).Fields.VocalHandling;

        Of("unplugged").ShouldBe(Strength.Light);
        Of("chiptune").ShouldBe(Strength.Strong);
        Of("house").ShouldBe(Strength.Balanced);
        Vocals("solo-piano").ShouldBe(VocalHandling.Instrumental);
        Vocals("reggae").ShouldBe(VocalHandling.Keep);
    }

    [Fact]
    public void Parse_RejectsAnEmptyDocument() =>
        Should.Throw<InvalidOperationException>(() => PresetCatalog.Parse("null"));
}
