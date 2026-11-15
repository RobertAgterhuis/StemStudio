using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Generations;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Styles;

/// <summary>009 T058: create, update, delete and duplicate styles (US3-1..4, FR-004, FR-005).</summary>
public sealed class StyleEditingTests : ApiTestBase
{
    [Fact]
    public async Task Create_StoresAUserStyle()
    {
        using var response = await PostAsync(ApiRoutes.Styles, Input());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var style = await response.BodyAsync<StyleDto>();
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.Style(style.Id));
        (style.Kind, style.PresetKey, style.Name, style.VocalHandling).ShouldBe((StyleKindDto.User, null, "Rainy cafe jazz", VocalHandlingDto.Instrumental));
        style.Moods.ShouldBe(["melancholic", "intimate"]);
        await ContractAssert.MatchesAsync(response, "createStyle");
    }

    [Fact]
    public async Task Create_FieldLimitsAre400()
    {
        using var response = await PostAsync(ApiRoutes.Styles, Input() with { Name = new string('n', 61), Moods = Enumerable.Range(0, 9).Select(i => "m" + i).ToList() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("name")).ShouldBe(["The name must be 1 to 60 characters."]);
        (await response.FieldErrorsAsync("moods")).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(response, "createStyle");
    }

    [Theory]
    [InlineData("description")]
    [InlineData("name")]
    [InlineData("subGenre")]
    public async Task Create_ImitationIsRefusedWithTypeImitation(string field)
    {
        var input = field switch
        {
            "name" => Input() with { Name = "In The Style Of Someone" },
            "subGenre" => Input() with { SubGenre = "sounds like radio" },
            _ => Input() with { Description = "Rustige piano in de stijl van een bekende zanger" },
        };

        using var response = await PostAsync(ApiRoutes.Styles, input);

        var problem = await RestyleEndpointTests.AssertProblemAsync(response, HttpStatusCode.BadRequest, "imitation");
        problem.GetProperty("title").GetString().ShouldBe("Styles cannot imitate an artist.");
        (await response.FieldErrorsAsync(field)).ShouldBe([Domain.Styles.StyleTextGuard.Message]);
        await ContractAssert.MatchesAsync(response, "createStyle");
    }

    [Fact]
    public async Task Create_ArtistsOfTheLibraryAreRefused()
    {
        var track = SyncTestData.UrlTrack();
        track.ApplyMetadata(new SourceMetadata("Song", "StemStudio Fixtures"), SyncTestData.T0);
        await Factory.SeedAsync(track);

        using var response = await PostAsync(ApiRoutes.Styles, Input() with { Description = "Bright synth pop like StemStudio Fixtures" });

        await RestyleEndpointTests.AssertProblemAsync(response, HttpStatusCode.BadRequest, "imitation");
    }

    [Fact]
    public async Task Create_TakenNamesAre409IgnoringCase()
    {
        using var preset = await PostAsync(ApiRoutes.Styles, Input() with { Name = "jazz TRIO" });

        (await RestyleEndpointTests.AssertProblemAsync(preset, HttpStatusCode.Conflict, "style-name-taken")).GetProperty("detail").GetString()
            .ShouldBe("A style named \"jazz TRIO\" already exists.");
        await ContractAssert.MatchesAsync(preset, "createStyle");
    }

    [Fact]
    public async Task Update_ReplacesAUserStyleAndKeepsGenerationSnapshots()
    {
        var style = await (await PostAsync(ApiRoutes.Styles, Input())).BodyAsync<StyleDto>();
        var track = await RestyleEndpointTests.SeedTrackAsync(Factory);
        var generation = await (await Client.PostAsJsonAsync(new Uri(ApiRoutes.Restyles(track.Id), UriKind.Relative), RestyleEndpointTests.Request() with { StyleId = style.Id }, Ct)).BodyAsync<GenerationDto>();

        using var response = await PutAsync(ApiRoutes.Style(style.Id), Input() with { Name = "Sunny cafe jazz" });

        (await response.BodyAsync<StyleDto>()).Name.ShouldBe("Sunny cafe jazz");
        await ContractAssert.MatchesAsync(response, "updateStyle");
        var stored = await (await Client.GetAsync(new Uri(ApiRoutes.Generation(generation.Id), UriKind.Relative), Ct)).BodyAsync<GenerationDto>();
        stored.Restyle!.Style.Name.ShouldBe("Rainy cafe jazz");
    }

    [Fact]
    public async Task Update_PresetMissingTakenAndInvalid()
    {
        var mine = await (await PostAsync(ApiRoutes.Styles, Input())).BodyAsync<StyleDto>();

        using var preset = await PutAsync(ApiRoutes.Style(RestyleEndpointTests.JazzTrio), Input());
        using var missing = await PutAsync(ApiRoutes.Style(Guid.NewGuid()), Input());
        using var taken = await PutAsync(ApiRoutes.Style(mine.Id), Input() with { Name = "Bossa nova" });
        using var invalid = await PutAsync(ApiRoutes.Style(mine.Id), Input() with { Description = "sounds like a famous tenor" });
        using var same = await PutAsync(ApiRoutes.Style(mine.Id), Input() with { Name = "RAINY cafe jazz" });

        await RestyleEndpointTests.AssertProblemAsync(preset, HttpStatusCode.Forbidden, "preset-read-only");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await RestyleEndpointTests.AssertProblemAsync(taken, HttpStatusCode.Conflict, "style-name-taken");
        await RestyleEndpointTests.AssertProblemAsync(invalid, HttpStatusCode.BadRequest, "imitation");
        same.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var response in new[] { preset, missing, taken, invalid, same })
        {
            await ContractAssert.MatchesAsync(response, "updateStyle");
        }
    }

    [Fact]
    public async Task Delete_UserStylesOnly()
    {
        var mine = await (await PostAsync(ApiRoutes.Styles, Input())).BodyAsync<StyleDto>();

        using var deleted = await DeleteAsync(ApiRoutes.Style(mine.Id));
        using var missing = await DeleteAsync(ApiRoutes.Style(mine.Id));
        using var preset = await DeleteAsync(ApiRoutes.Style(RestyleEndpointTests.JazzTrio));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await RestyleEndpointTests.AssertProblemAsync(preset, HttpStatusCode.Forbidden, "preset-read-only");
        foreach (var response in new[] { deleted, missing, preset })
        {
            await ContractAssert.MatchesAsync(response, "deleteStyle");
        }
    }

    [Fact]
    public async Task Duplicate_MakesNumberedUserCopies()
    {
        using var first = await PostAsync(ApiRoutes.DuplicateStyle(RestyleEndpointTests.JazzTrio), null);
        using var second = await PostAsync(ApiRoutes.DuplicateStyle(RestyleEndpointTests.JazzTrio), null);
        using var missing = await PostAsync(ApiRoutes.DuplicateStyle(Guid.NewGuid()), null);

        var copy = await first.BodyAsync<StyleDto>();
        (copy.Kind, copy.PresetKey, copy.Name, copy.SubGenre).ShouldBe((StyleKindDto.User, null, "Jazz trio (copy)", "swing"));
        first.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.Style(copy.Id));
        (await second.BodyAsync<StyleDto>()).Name.ShouldBe("Jazz trio (copy 2)");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var response in new[] { first, second, missing })
        {
            await ContractAssert.MatchesAsync(response, "duplicateStyle");
        }
    }

    internal static StyleInput Input() => new()
    {
        Name = "Rainy cafe jazz",
        GenreFamily = GenreFamilyDto.Jazz,
        SubGenre = "cool jazz",
        TempoFeel = TempoFeelDto.Laidback,
        Instrumentation = ["muted trumpet", "piano", "upright bass"],
        Moods = ["melancholic", "intimate"],
        VocalHandling = VocalHandlingDto.Instrumental,
        DefaultStrength = StrengthDto.Light,
        Description = "Late-night trio with a soft muted trumpet.",
    };

    private Task<HttpResponseMessage> PostAsync(string path, StyleInput? input) =>
        input is null ? Client.PostAsync(new Uri(path, UriKind.Relative), null, Ct) : Client.PostAsJsonAsync(new Uri(path, UriKind.Relative), input, Ct);

    private Task<HttpResponseMessage> PutAsync(string path, StyleInput input) => Client.PutAsJsonAsync(new Uri(path, UriKind.Relative), input, Ct);

    private Task<HttpResponseMessage> DeleteAsync(string path) => Client.DeleteAsync(new Uri(path, UriKind.Relative), Ct);
}
