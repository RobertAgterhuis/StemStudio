using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Generations;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Styles;
using StemStudio.Domain.Styles;

namespace StemStudio.Api.Tests.Styles;

/// <summary>009 T046: the style library read side (FR-003, FR-004, SC-003).</summary>
public sealed class StyleEndpointTests : ApiTestBase
{
    [Fact]
    public async Task List_PresetsFirstThenByName()
    {
        await RestyleEndpointTests.SeedUserStyleAsync(Factory, name: "aaa first user");
        await RestyleEndpointTests.SeedUserStyleAsync(Factory, name: "Zed");

        using var response = await GetAsync(ApiRoutes.Styles);

        var styles = await response.BodyAsync<List<StyleDto>>();
        styles.Count.ShouldBe(26);
        styles.Take(24).ShouldAllBe(style => style.Kind == StyleKindDto.Preset);
        styles.Take(24).Select(style => style.Name).ShouldBe(styles.Take(24).Select(style => style.Name).OrderBy(name => name.ToUpperInvariant(), StringComparer.Ordinal));
        styles.Skip(24).Select(style => style.Name).ShouldBe(["aaa first user", "Zed"]);
        styles.Select(style => style.GenreFamily).Distinct().Count().ShouldBeGreaterThanOrEqualTo(8);
        await ContractAssert.MatchesAsync(response, "listStyles");
    }

    [Theory]
    [InlineData("kind=user", 1)]
    [InlineData("kind=preset", 24)]
    [InlineData("genreFamily=electronic", 4)]
    [InlineData("genreFamily=hipHop", 2)]
    [InlineData("mood=nostalgic", 2)]
    [InlineData("instrument=PIANO", 2)]
    [InlineData("instrument=grand%20piano", 1)]
    [InlineData("q=JAZZ", 3)]
    [InlineData("q=walking%20bass&genreFamily=jazz", 2)]
    [InlineData("kind=preset&mood=melancholic", 0)]
    public async Task List_FiltersCombine(string query, int expected)
    {
        await RestyleEndpointTests.SeedUserStyleAsync(Factory);

        using var response = await GetAsync($"{ApiRoutes.Styles}?{query}");

        (await response.BodyAsync<List<StyleDto>>()).Count.ShouldBe(expected);
        await ContractAssert.MatchesAsync(response, "listStyles");
    }

    [Theory]
    [InlineData("kind=nope", "kind")]
    [InlineData("genreFamily=polka", "genreFamily")]
    [InlineData("mood=1234567890123456789012345678901", "mood")]
    public async Task List_InvalidFiltersAre400(string query, string field)
    {
        using var response = await GetAsync($"{ApiRoutes.Styles}?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(response, "listStyles");
    }

    [Fact]
    public async Task Get_PresetUserAndMissing()
    {
        var user = await RestyleEndpointTests.SeedUserStyleAsync(Factory);

        using var preset = await GetAsync(ApiRoutes.Style(RestyleEndpointTests.JazzTrio));
        using var mine = await GetAsync(ApiRoutes.Style(user.Id));
        using var missing = await GetAsync(ApiRoutes.Style(Guid.NewGuid()));

        var jazz = await preset.BodyAsync<StyleDto>();
        (jazz.PresetKey, jazz.Name, jazz.SubGenre, jazz.TempoFeel, jazz.DefaultStrength).ShouldBe(("jazz-trio", "Jazz trio", "swing", TempoFeelDto.Swing, StrengthDto.Balanced));
        jazz.Instrumentation.ShouldBe(["piano", "upright bass", "brushed drums"]);
        (await mine.BodyAsync<StyleDto>()).Kind.ShouldBe(StyleKindDto.User);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var response in new[] { preset, mine, missing })
        {
            await ContractAssert.MatchesAsync(response, "getStyle");
        }
    }

    [Fact]
    public void Presets_AreAllInTheLibrary() => PresetCatalog.Load().Presets.Count.ShouldBe(24);

    private Task<HttpResponseMessage> GetAsync(string path) => Client.GetAsync(new Uri(path, UriKind.Relative), Ct);
}
