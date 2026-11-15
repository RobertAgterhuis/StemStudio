using StemStudio.Domain.Artists;

namespace StemStudio.Domain.Tests.Artists;

/// <summary>Feature 005 T016: artist value limits (data-model 2.5, 3.3) and the 30-day refresh.</summary>
public sealed class ArtistTests
{
    public static readonly DateTime T0 = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    public static readonly Guid Mbid = Guid.Parse("6b7c8d9e-0f1a-4b2c-8d3e-4f5a6b7c8d9e");

    public static ArtistFacts Facts() => new()
    {
        Mbid = Mbid,
        Name = "The Example Band",
        Type = ArtistType.Group,
        Country = "US",
        BeginYear = 1981,
        Genres = ["heavy metal", "thrash metal"],
        Members = [new ArtistMember("First Member", 1981), new ArtistMember("Second Member", 1983, 2001)],
        Website = "https://www.example.org/",
        WikidataId = "Q15920",
        Summary = new WikipediaSummary(
            "en",
            "The Example Band",
            "A placeholder group.",
            "https://en.wikipedia.org/wiki/The_Example_Band",
            "https://en.wikipedia.org/w/index.php?title=The_Example_Band&action=history",
            "1255000000"),
        FetchedAt = T0,
    };

    [Fact]
    public void Create_CopiesEveryFact()
    {
        var artist = Artist.Create(Facts());

        artist.Mbid.ShouldBe(Mbid);
        artist.Name.ShouldBe("The Example Band");
        artist.Type.ShouldBe(ArtistType.Group);
        artist.Country.ShouldBe("US");
        artist.BeginYear.ShouldBe(1981);
        artist.EndYear.ShouldBeNull();
        artist.Genres.ShouldBe(["heavy metal", "thrash metal"]);
        artist.Members.Count.ShouldBe(2);
        artist.Members[1].ShouldBe(new ArtistMember("Second Member", 1983, 2001));
        artist.Website.ShouldBe("https://www.example.org/");
        artist.WikidataId.ShouldBe("Q15920");
        artist.Summary!.Title.ShouldBe("The Example Band");
        artist.FetchedAt.ShouldBe(T0);
    }

    [Fact]
    public void Create_AcceptsTheMinimalFacts()
    {
        var artist = Artist.Create(new ArtistFacts { Mbid = Mbid, Name = "Solo", FetchedAt = T0 });

        artist.Type.ShouldBeNull();
        artist.Genres.ShouldBeEmpty();
        artist.Members.ShouldBeEmpty();
        artist.Summary.ShouldBeNull();
    }

    public static TheoryData<string, Func<ArtistFacts, ArtistFacts>> Violations() => new()
    {
        { "mbid", facts => facts with { Mbid = Guid.Empty } },
        { "name", facts => facts with { Name = " " } },
        { "name", facts => facts with { Name = new string('n', 501) } },
        { "type", facts => facts with { Type = (ArtistType)99 } },
        { "country", facts => facts with { Country = "usa" } },
        { "beginYear", facts => facts with { BeginYear = 999 } },
        { "endYear", facts => facts with { EndYear = 3000 } },
        { "genres", facts => facts with { Genres = Enumerable.Range(0, 9).Select(i => $"g{i}").ToList() } },
        { "genres[0]", facts => facts with { Genres = [new string('g', 101)] } },
        { "members", facts => facts with { Members = Enumerable.Range(0, 13).Select(i => new ArtistMember($"m{i}")).ToList() } },
        { "members[0].name", facts => facts with { Members = [new ArtistMember("")] } },
        { "members[0].fromYear", facts => facts with { Members = [new ArtistMember("A", 12)] } },
        { "members[0].toYear", facts => facts with { Members = [new ArtistMember("A", null, 5000)] } },
        { "website", facts => facts with { Website = "http://insecure.example" } },
        { "wikidataId", facts => facts with { WikidataId = "P31" } },
        { "summary.language", facts => facts with { Summary = facts.Summary! with { Language = "EN" } } },
        { "summary.title", facts => facts with { Summary = facts.Summary! with { Title = "" } } },
        { "summary.extract", facts => facts with { Summary = facts.Summary! with { Extract = new string('e', 2001) } } },
        { "summary.extract", facts => facts with { Summary = facts.Summary! with { Extract = " " } } },
        { "summary.pageUrl", facts => facts with { Summary = facts.Summary! with { PageUrl = "ftp://x" } } },
        { "summary.historyUrl", facts => facts with { Summary = facts.Summary! with { HistoryUrl = "x" } } },
    };

    [Theory]
    [MemberData(nameof(Violations))]
    public void Create_RejectsValuesOutsideTheLimits(string field, Func<ArtistFacts, ArtistFacts> change)
    {
        var error = Should.Throw<DomainValidationException>(() => Artist.Create(change(Facts())));

        error.Errors.ShouldContain(e => e.Field == field);
    }

    [Fact]
    public void Create_RejectsNull()
    {
        Should.Throw<ArgumentNullException>(() => Artist.Create(null!));
    }

    [Fact]
    public void IsStale_After30Days()
    {
        var artist = Artist.Create(Facts());

        artist.IsStale(T0.AddDays(29)).ShouldBeFalse();
        artist.IsStale(T0.AddDays(30)).ShouldBeTrue();
    }

    [Fact]
    public void Refresh_ReplacesTheFacts()
    {
        var artist = Artist.Create(Facts());

        artist.Refresh(Facts() with { Name = "Renamed", Genres = [], Members = [], Summary = null, FetchedAt = T0.AddDays(31) });

        artist.Name.ShouldBe("Renamed");
        artist.Genres.ShouldBeEmpty();
        artist.Members.ShouldBeEmpty();
        artist.Summary.ShouldBeNull();
        artist.FetchedAt.ShouldBe(T0.AddDays(31));
    }

    [Fact]
    public void Refresh_RejectsAnotherArtistAndInvalidFacts()
    {
        var artist = Artist.Create(Facts());

        Should.Throw<ArgumentException>(() => artist.Refresh(Facts() with { Mbid = Guid.NewGuid() }));
        Should.Throw<DomainValidationException>(() => artist.Refresh(Facts() with { Name = "" }));
        artist.Name.ShouldBe("The Example Band");
    }
}
