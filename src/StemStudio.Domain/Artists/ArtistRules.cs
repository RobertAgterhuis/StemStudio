using System.Globalization;
using System.Text.RegularExpressions;

namespace StemStudio.Domain.Artists;

/// <summary>Value limits of <see cref="ArtistFacts"/> (data-model 2.5 and 2.6; backend contract <c>ArtistInfo</c>).</summary>
internal static partial class ArtistRules
{
    /// <summary>Most genres.</summary>
    public const int MaxGenres = 8;

    /// <summary>Longest genre.</summary>
    public const int MaxGenreLength = 100;

    /// <summary>Most members.</summary>
    public const int MaxMembers = 12;

    /// <summary>Longest name, member name and article title.</summary>
    public const int MaxNameLength = 500;

    /// <summary>Longest Wikipedia excerpt.</summary>
    public const int MaxExtractLength = 2000;

    private static readonly Bounds Years = new(1000, 2999);

    /// <summary>Throws when a fact breaks a rule.</summary>
    /// <param name="facts">The facts.</param>
    /// <exception cref="DomainValidationException">A rule is violated.</exception>
    public static void Validate(ArtistFacts facts)
    {
        var errors = new ValidationErrors();
        CheckIdentity(errors, facts);
        CheckLists(errors, facts);
        CheckLinks(errors, facts);
        if (facts.Summary is { } summary)
        {
            CheckSummary(errors.Nested("summary"), summary);
        }

        errors.ThrowIfAny();
    }

    private static void CheckIdentity(ValidationErrors errors, ArtistFacts facts)
    {
        errors.Check(facts.Mbid != Guid.Empty, "mbid", "Must be a MusicBrainz id.");
        CheckText(errors, "name", facts.Name, MaxNameLength);
        errors.Check(facts.Type is null || Enum.IsDefined(facts.Type.Value), "type", "Must be a known artist type.");
        errors.Check(facts.Country is null || CountryPattern().IsMatch(facts.Country), "country", "Must be an ISO 3166-1 alpha-2 code.");
        CheckYear(errors, "beginYear", facts.BeginYear);
        CheckYear(errors, "endYear", facts.EndYear);
    }

    private static void CheckLists(ValidationErrors errors, ArtistFacts facts)
    {
        errors.Check(facts.Genres.Count <= MaxGenres, "genres", $"Must have at most {MaxGenres} entries.");
        for (var i = 0; i < facts.Genres.Count; i++)
        {
            CheckText(errors, Indexed("genres", i), facts.Genres[i], MaxGenreLength);
        }

        errors.Check(facts.Members.Count <= MaxMembers, "members", $"Must have at most {MaxMembers} entries.");
        for (var i = 0; i < facts.Members.Count; i++)
        {
            CheckMember(errors.Nested(Indexed("members", i)), facts.Members[i]);
        }
    }

    private static void CheckMember(ValidationErrors errors, ArtistMember member)
    {
        CheckText(errors, "name", member.Name, MaxNameLength);
        CheckYear(errors, "fromYear", member.FromYear);
        CheckYear(errors, "toYear", member.ToYear);
    }

    private static void CheckLinks(ValidationErrors errors, ArtistFacts facts)
    {
        errors.Check(facts.Website is null || IsHttps(facts.Website), "website", "Must be an https URL.");
        errors.Check(facts.WikidataId is null || WikidataPattern().IsMatch(facts.WikidataId), "wikidataId", "Must be a Wikidata item id.");
    }

    private static void CheckSummary(ValidationErrors errors, WikipediaSummary summary)
    {
        errors.Check(summary.Language is not null && LanguagePattern().IsMatch(summary.Language), "language", "Must be a 2-3 letter wiki code.");
        CheckText(errors, "title", summary.Title, MaxNameLength);
        CheckText(errors, "extract", summary.Extract, MaxExtractLength);
        errors.Check(IsHttps(summary.PageUrl), "pageUrl", "Must be an https URL.");
        errors.Check(IsHttps(summary.HistoryUrl), "historyUrl", "Must be an https URL.");
    }

    private static void CheckText(ValidationErrors errors, string field, string? value, int maxLength) =>
        errors.Check(!string.IsNullOrWhiteSpace(value) && value.Length <= maxLength, field, $"Must be 1 to {maxLength} characters long.");

    private static void CheckYear(ValidationErrors errors, string field, int? year) =>
        errors.Check(year is null || Years.Contains(year.Value), field, Years.Message);

    private static bool IsHttps(string? url) => url is not null && url.StartsWith("https://", StringComparison.Ordinal);

    private static string Indexed(string field, int index) => string.Create(CultureInfo.InvariantCulture, $"{field}[{index}]");

    [GeneratedRegex(@"\A[A-Z]{2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CountryPattern();

    [GeneratedRegex(@"\AQ[0-9]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex WikidataPattern();

    [GeneratedRegex(@"\A[a-z]{2,3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex LanguagePattern();
}
