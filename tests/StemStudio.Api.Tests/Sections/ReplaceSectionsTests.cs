using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Sections;
using StemStudio.Domain.Sections;

namespace StemStudio.Api.Tests.SongSections;

/// <summary>Feature 014 T070: <c>replaceSections</c> (FR-006, data-model 3.2).</summary>
public sealed class ReplaceSectionsTests : ApiTestBase
{
    private static SectionInputDto Input(double start, double end, SectionKindDto kind = SectionKindDto.Verse, string label = "Verse") =>
        new() { Kind = kind, Label = label, StartSeconds = start, EndSeconds = end };

    [Fact]
    public async Task Replace_KeepsSentIds_GivesNewOnes_AndMarksTheSetEdited()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        var stored = await Factory.SeedDetectedAsync(track.Id);
        var (intro, verse) = (stored.Sections[0], stored.Sections[1]);

        using var response = await PutAsync(track.Id, 1, Input(0, 8.1, SectionKindDto.Intro, "Intro") with { Id = intro.Id }, Input(8.1, 30, label: "Verse A") with { Id = verse.Id }, Input(30, 64, SectionKindDto.Solo, "Bass solo") with { Instrument = "bass" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "replaceSections");
        var set = await response.BodyAsync<SectionSetDto>();
        (set.Origin, set.Revision, set.Status).ShouldBe((SectionSetOriginDto.Edited, 2, SectionSetStatusDto.Ready));
        set.Sections.Select(section => section.Origin).ShouldBe([SectionOriginDto.Detected, SectionOriginDto.User, SectionOriginDto.User]);
        set.Sections[0].Id.ShouldBe(intro.Id);
        set.Sections[1].Id.ShouldBe(verse.Id);
        set.Sections[2].Id.ShouldNotBe(Guid.Empty);
        set.Sections[2].Instrument.ShouldBe("bass");
        set.Sections[1].Confidence.ShouldBeNull();
        (await Factory.FindSectionsAsync(track.Id))!.Sections.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Replace_WithoutAStoredSet_StartsAtRevisionZero()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());

        using var response = await PutAsync(track.Id, 0, Input(0, 30));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var set = await response.BodyAsync<SectionSetDto>();
        (set.Revision, set.Origin, set.Sections.Single().Origin).ShouldBe((1, SectionSetOriginDto.Edited, SectionOriginDto.User));
    }

    [Fact]
    public async Task Replace_WithAStaleRevision_Is409SectionsChanged_AndStoresNothing()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        await Factory.SeedDetectedAsync(track.Id);
        using (var first = await PutAsync(track.Id, 1, Input(0, 30)))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var second = await PutAsync(track.Id, 1, Input(0, 40));

        await SectionEndpointsTests.AssertProblemAsync(second, HttpStatusCode.Conflict, "sections-changed");
        await ContractAssert.MatchesAsync(second, "replaceSections");
        (await Factory.FindSectionsAsync(track.Id))!.Sections.Single().EndSeconds.ShouldBe(30);
    }

    public static TheoryData<string, SectionInputDto[]> InvalidLists => new()
    {
        { "sections", [] },
        { "sections[0].label", [Input(0, 10, label: " ")] },
        { "sections[0].label", [Input(0, 10, label: new string('x', 41))] },
        { "sections[0].startSeconds", [Input(-1, 10)] },
        { "sections[0].endSeconds", [Input(200, 214)] },
        { "sections[0].endSeconds", [Input(5, 5.5)] },
        { "sections[1].startSeconds", [Input(0, 10), Input(9, 20)] },
        { "sections[0].instrument", [Input(0, 10) with { Instrument = "bass" }] },
        { "sections[0].instrument", [Input(0, 10, SectionKindDto.Solo, "Solo") with { Instrument = "guitar.electric-guitar" }] },
        { "sections[0].id", [Input(0, 10) with { Id = Guid.NewGuid() }] },
    };

    [Theory]
    [MemberData(nameof(InvalidLists))]
    public async Task Replace_BrokenRules_Are422WithTheField(string field, SectionInputDto[] sections)
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());

        using var response = await PutAsync(track.Id, 0, sections);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "replaceSections");
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        (await Factory.FindSectionsAsync(track.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Replace_MoreThan64Sections_Is422()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());

        using var response = await PutAsync(track.Id, 0, Enumerable.Range(0, 65).Select(i => Input(i * 3, (i * 3) + 2)).ToArray());

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.FieldErrorsAsync("sections")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Replace_UnknownTrack_Is404()
    {
        using var response = await PutAsync(Guid.NewGuid(), 0, Input(0, 10));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Replace_ADetailSoloOfASplitTrack_IsAccepted()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());
        var channel = SectionChannels.Of(track).First(id => id.Contains('.', StringComparison.Ordinal));

        using var response = await PutAsync(track.Id, 0, Input(0, 10, SectionKindDto.Solo, "Solo") with { Instrument = channel });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Detect_AfterAnEdit_NeedsOverwrite_AndThenReplacesTheEdits()
    {
        var track = await Factory.SeedAsync(SectionTestData.Completed());
        using (var edit = await PutAsync(track.Id, 0, Input(0, 30, label: "Mine")))
        {
            edit.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var refused = await Client.PostAsJsonAsync(ApiRoutes.SectionsDetect(track.Id), new DetectSectionsRequest(), Ct);
        await SectionEndpointsTests.AssertProblemAsync(refused, HttpStatusCode.Conflict, "sections-edited");
        using var allowed = await Client.PostAsJsonAsync(ApiRoutes.SectionsDetect(track.Id), new DetectSectionsRequest { Overwrite = true }, Ct);
        allowed.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Factory.PracticeWorker.CompleteSection(Factory.PracticeWorker.SectionJobs.Keys.Single());

        await Factory.SyncSectionsAsync();

        var set = (await Factory.FindSectionsAsync(track.Id))!;
        (set.Origin, set.Revision, set.Sections.Count).ShouldBe((SectionSetOrigin.Detected, 2, 5));
        set.Sections.ShouldAllBe(section => section.Label != "Mine");
    }

    private Task<HttpResponseMessage> PutAsync(Guid trackId, int revision, params SectionInputDto[] sections) =>
        Client.PutAsJsonAsync(ApiRoutes.Sections(trackId), new SectionsUpdateRequest { Revision = revision, Sections = sections }, Ct);
}
