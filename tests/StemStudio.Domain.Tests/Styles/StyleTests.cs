using StemStudio.Domain.Styles;

namespace StemStudio.Domain.Tests.Styles;

public sealed class StyleTests
{
    private static readonly DateTime T1 = StyleTestData.T0.AddHours(1);

    [Fact]
    public void CreateUser_NormalisesTextAndStoresFields()
    {
        var fields = StyleTestData.RainyCafe() with { Name = "  Rainy cafe jazz ", SubGenre = "  ", Description = " x ", Moods = [" calm ", "calm"] };

        var style = Style.CreateUser(Guid.NewGuid(), fields, StyleTestData.T0);

        style.Kind.ShouldBe(StyleKind.User);
        style.PresetKey.ShouldBeNull();
        style.Name.ShouldBe("Rainy cafe jazz");
        style.NormalizedName.ShouldBe("RAINY CAFE JAZZ");
        style.SubGenre.ShouldBeNull();
        style.Description.ShouldBe("x");
        style.Moods.ShouldBe(["calm"]);
        style.Instrumentation.ShouldBe(["muted trumpet", "piano", "upright bass"]);
        style.CreatedAt.ShouldBe(StyleTestData.T0);
        style.UpdatedAt.ShouldBe(StyleTestData.T0);
        style.IsPreset.ShouldBeFalse();
        style.Fields.Name.ShouldBe("Rainy cafe jazz");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901")]
    public void CreateUser_RejectsBadNames(string name)
    {
        var error = Should.Throw<DomainValidationException>(() => StyleTestData.User(StyleTestData.RainyCafe() with { Name = name }));

        error.Errors.ShouldContain(e => e.Field == "name" && e.Message == "The name must be 1 to 60 characters.");
    }

    [Fact]
    public void Validate_ReportsEveryLimit()
    {
        var tags = Enumerable.Range(0, 9).Select(i => "tag" + i).ToList();
        var fields = StyleTestData.RainyCafe() with
        {
            SubGenre = new string('s', 41),
            Instrumentation = tags,
            Moods = [new string('m', 31)],
            Description = new string('d', 301),
        };

        var errors = StyleRules.Validate(fields.Normalized());

        errors.Select(e => e.Field).ShouldBe(["subGenre", "instrumentation", "moods", "description"], ignoreOrder: true);
    }

    [Fact]
    public void Validate_AcceptsAValidStyle() => StyleRules.Validate(StyleTestData.RainyCafe()).ShouldBeEmpty();

    [Fact]
    public void Update_ReplacesFieldsOfAUserStyle()
    {
        var style = StyleTestData.User();

        style.Update(StyleTestData.JazzTrio() with { Name = "Mine" }, T1);

        style.Name.ShouldBe("Mine");
        style.GenreFamily.ShouldBe(GenreFamily.Jazz);
        style.TempoFeel.ShouldBe(TempoFeel.Swing);
        style.VocalHandling.ShouldBe(VocalHandling.Keep);
        style.DefaultStrength.ShouldBe(Strength.Balanced);
        style.UpdatedAt.ShouldBe(T1);
        style.CreatedAt.ShouldBe(StyleTestData.T0);
    }

    [Fact]
    public void Update_PresetIsReadOnly()
    {
        var style = StyleTestData.Preset();

        Should.Throw<StyleReadOnlyException>(() => style.Update(StyleTestData.RainyCafe(), T1));
    }

    [Fact]
    public void Update_InvalidFieldsThrow()
    {
        var style = StyleTestData.User();

        Should.Throw<DomainValidationException>(() => style.Update(StyleTestData.RainyCafe() with { Name = " " }, T1));
    }

    [Fact]
    public void FromPreset_IsReadOnlyWithKey()
    {
        var style = StyleTestData.Preset();

        style.Kind.ShouldBe(StyleKind.Preset);
        style.IsPreset.ShouldBeTrue();
        style.PresetKey.ShouldBe("jazz-trio");
        style.Id.ShouldBe(Guid.Parse("3f9c2a1e-6b4d-4e8f-a0c2-1d3e5f7a9b01"));
    }

    [Fact]
    public void ApplyPreset_UpdatesOnlyWhenChanged()
    {
        var style = StyleTestData.Preset();
        var same = new PresetDefinition(style.Id, "jazz-trio", StyleTestData.JazzTrio());

        style.ApplyPreset(same, T1).ShouldBeFalse();
        style.UpdatedAt.ShouldBe(StyleTestData.T0);
        style.ApplyPreset(same with { Fields = StyleTestData.JazzTrio() with { Moods = ["cosy"] } }, T1).ShouldBeTrue();
        style.Moods.ShouldBe(["cosy"]);
        style.UpdatedAt.ShouldBe(T1);
    }

    [Fact]
    public void ApplyPreset_RejectsAUserStyle()
    {
        var style = StyleTestData.User();

        Should.Throw<StyleReadOnlyException>(() => style.ApplyPreset(new PresetDefinition(style.Id, "k", StyleTestData.JazzTrio()), T1));
    }

    [Fact]
    public void Snapshot_CopiesEveryField()
    {
        var style = StyleTestData.Preset();

        var snapshot = style.Snapshot();

        snapshot.StyleId.ShouldBe(style.Id);
        snapshot.Kind.ShouldBe(StyleKind.Preset);
        snapshot.PresetKey.ShouldBe("jazz-trio");
        snapshot.Fields.ShouldBe(style.Fields);
        snapshot.Name.ShouldBe("Jazz trio");
    }

    [Fact]
    public void Fields_EqualityComparesTagsByValue()
    {
        StyleTestData.JazzTrio().ShouldBe(StyleTestData.JazzTrio());
        StyleTestData.JazzTrio().ShouldNotBe(StyleTestData.JazzTrio() with { Moods = ["warm"] });
        StyleTestData.JazzTrio().ShouldNotBe(StyleTestData.JazzTrio() with { Instrumentation = ["warm"] });
        StyleTestData.JazzTrio().ShouldNotBe(StyleTestData.JazzTrio() with { Name = "x" });
        StyleTestData.JazzTrio().GetHashCode().ShouldBe(StyleTestData.JazzTrio().GetHashCode());
        StyleTestData.JazzTrio().Equals(null).ShouldBeFalse();
    }

    /// <summary>Filters (kind, family, mood, instrument, query) and how many of the two test styles match.</summary>
    public static TheoryData<StyleFilter, int> FilterCases => new()
    {
        { new StyleFilter(StyleKind.Preset, null, null, null, null), 1 },
        { new StyleFilter(StyleKind.User, null, null, null, null), 1 },
        { new StyleFilter(null, GenreFamily.Jazz, null, null, null), 2 },
        { new StyleFilter(null, GenreFamily.Rock, null, null, null), 0 },
        { new StyleFilter(null, null, "WARM", null, null), 1 },
        { new StyleFilter(null, null, null, "Piano", null), 2 },
        { new StyleFilter(null, null, null, null, "late-NIGHT"), 1 },
        { new StyleFilter(null, null, null, null, "upright"), 2 },
        { new StyleFilter(null, null, null, null, "rainy"), 1 },
        { new StyleFilter(null, null, null, null, "relaxed"), 1 },
        { new StyleFilter(null, null, null, null, "nothing"), 0 },
        { new StyleFilter(null, null, " ", "", "  "), 2 },
    };

    [Theory]
    [MemberData(nameof(FilterCases))]
    public void Filter_CombinesCriteria(StyleFilter filter, int expected)
    {
        var styles = new[] { StyleTestData.Preset(), StyleTestData.User() };

        styles.Count(filter.Matches).ShouldBe(expected);
    }

    [Fact]
    public void Filter_HandlesAStyleWithoutDescription()
    {
        var style = StyleTestData.User(StyleTestData.RainyCafe() with { Description = null });

        new StyleFilter(null, null, null, null, "trumpet").Matches(style).ShouldBeTrue();
        new StyleFilter(null, null, null, null, "late").Matches(style).ShouldBeFalse();
    }

    [Fact]
    public void Ordering_PresetsFirstThenName()
    {
        var b = StyleTestData.User(StyleTestData.RainyCafe() with { Name = "beta" });
        var a = StyleTestData.User(StyleTestData.RainyCafe() with { Name = "Alpha" });
        var preset = StyleTestData.Preset();

        StyleFilter.Order([b, preset, a]).ShouldBe([preset, a, b]);
    }

    [Fact]
    public void CopyName_NumbersWhenTaken()
    {
        StyleNaming.CopyName("Jazz trio", []).ShouldBe("Jazz trio (copy)");
        StyleNaming.CopyName("Jazz trio", ["jazz TRIO (copy)"]).ShouldBe("Jazz trio (copy 2)");
        StyleNaming.CopyName(new string('n', 60), []).Length.ShouldBe(60);
    }

    [Fact]
    public void RestoredName_UsesTheRestoredSuffix_AndRenameKeepsTheFields()
    {
        StyleNaming.RestoredName("Jazz trio", ["JAZZ TRIO"]).ShouldBe("Jazz trio (restored)");
        StyleNaming.RestoredName("Jazz trio", ["jazz trio (restored)"]).ShouldBe("Jazz trio (restored 2)");
        StyleNaming.RestoredName(new string('n', 60), []).Length.ShouldBe(60);
        var style = StyleTestData.User();
        var fields = style.Fields;

        style.RenameRestored("Jazz trio (restored)");

        (style.Name, style.NormalizedName).ShouldBe(("Jazz trio (restored)", "JAZZ TRIO (RESTORED)"));
        style.Fields.ShouldBe(fields with { Name = "Jazz trio (restored)" });
    }

    [Fact]
    public void RenameRestored_OfAPreset_IsRefused() =>
        Should.Throw<StyleReadOnlyException>(() => StyleTestData.Preset().RenameRestored("x"));
}
