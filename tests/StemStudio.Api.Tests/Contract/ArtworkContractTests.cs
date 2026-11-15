using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, features 005 + 007 Phase 0, 005 T003): artwork and band-info DTOs and routes against
/// backend-api 1.4.0 (feature 005), and the rule that the 007 document (1.5.0) contains 1.4.0 unchanged.
/// </summary>
public sealed class ArtworkContractTests
{
    private static readonly Lazy<OpenApiContract> Feature005 =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "005", "backend-api.openapi.yaml")));

    [Fact]
    public void Feature005Contract_IsVersion140()
    {
        Feature005.Value.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.4.0");
    }

    [Theory]
    [InlineData("paths")]
    [InlineData("components/schemas")]
    [InlineData("components/responses")]
    [InlineData("components/examples")]
    [InlineData("components/parameters")]
    [InlineData("components/headers")]
    public void Feature007Contract_ContainsEveryFeature005EntryUnchanged(string section)
    {
        // 006/008 Phase 0: the 007 document is linked as Contract/007; LyricsContractTests proves 007 -> 006 -> 008.
        ChangedEntries(Feature005.Value, Backend007, section).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(typeof(ArtworkSourceDto), "ArtworkSource")]
    [InlineData(typeof(MetadataStatusDto), "MetadataStatus")]
    [InlineData(typeof(ArtistTypeDto), "ArtistType")]
    public void Enums_MatchTheContract(Type enumType, string schema)
    {
        WireNames(enumType).ShouldBe(Strings(Schema(schema)["enum"]!));
    }

    [Fact]
    public void CompletedTrackExample_CarriesArtworkAndMetadataAndRoundTrips()
    {
        var json = Example("CompletedTrack");

        var dto = json.Deserialize<TrackDto>(Web)!;
        var again = JsonSerializer.SerializeToNode(dto, Web)!;

        dto.Artwork.ShouldBe(new ArtworkDto(ArtworkSourceDto.Spotify, "2c26b46b68ff", ApiRoutes.TrackArtwork(dto.Id, ApiRoutes.ArtworkCover, "2c26b46b68ff"), ApiRoutes.TrackArtwork(dto.Id, ApiRoutes.ArtworkThumb, "2c26b46b68ff")));
        dto.Metadata.Status.ShouldBe(MetadataStatusDto.Found);
        (dto.Metadata.Album, dto.Metadata.Year, dto.Metadata.ArtistName).ShouldBe(("Whenever You Need Somebody", 1987, "Rick Astley"));
        JsonNode.DeepEquals(again["metadata"], json["metadata"]).ShouldBeTrue();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Track"), again).ShouldBeEmpty();
    }

    [Fact]
    public void TracksWithoutLookup_SerializeMetadataNoneAndANullArtwork()
    {
        var dto = Example("QueuedTrack").Deserialize<TrackDto>(Web)! with { Metadata = TrackMetadataDto.None };
        var node = JsonSerializer.SerializeToNode(dto, Web)!;

        node["artwork"].ShouldBeNull();
        node["metadata"]!["status"]!.GetValue<string>().ShouldBe("none");
        node["metadata"]!["correctedByUser"]!.GetValue<bool>().ShouldBeFalse();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Track"), node).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("FoundBandInfo")]
    [InlineData("SearchingBandInfo")]
    [InlineData("FailedBandInfo")]
    public void BandInfoExamples_RoundTripThroughBandInfoDto(string example)
    {
        var json = Example(example);

        var again = JsonSerializer.SerializeToNode(json.Deserialize<BandInfoDto>(Web), Web);

        JsonNode.DeepEquals(again, json).ShouldBeTrue();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("BandInfo"), again).ShouldBeEmpty();
    }

    [Fact]
    public void FoundBandInfo_CarriesTheWikipediaAttributionConstants()
    {
        var dto = Example("FoundBandInfo").Deserialize<BandInfoDto>(Web)!;
        var summary = Schema("WikipediaSummary")["properties"]!;

        dto.Artist!.Summary!.Licence.ShouldBe(WikipediaSummaryDto.CcBySa40);
        summary["licence"]!["const"]!.GetValue<string>().ShouldBe(WikipediaSummaryDto.CcBySa40);
        summary["licenceUrl"]!["const"]!.GetValue<string>().ShouldBe(WikipediaSummaryDto.CcBySa40Url);
        dto.Sources.Select(s => s.Name).ShouldBe([DataSourceDto.MusicBrainz, DataSourceDto.Wikidata, DataSourceDto.Wikipedia, DataSourceDto.CoverArtArchive]);
        Strings(Schema("DataSource")["properties"]!["name"]!["enum"]!).ShouldBe(dto.Sources.Select(s => s.Name));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Hero of the Day", "Metallica")]
    public void MetadataLookupRequest_MatchesTheSchema(string? title, string? artist)
    {
        var node = JsonSerializer.SerializeToNode(new MetadataLookupRequest(title, artist), Web);

        SchemaValidator.Validate(OpenApiContract.Backend, Schema("MetadataLookupRequest"), node).ShouldBeEmpty();
    }

    [Fact]
    public void Routes_AreContractPaths()
    {
        var id = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

        ApiRoutes.TrackArtwork(id, ApiRoutes.ArtworkThumb).ShouldBe($"/api/tracks/{id}/artwork/thumb");
        ApiRoutes.TrackArtwork(id, ApiRoutes.ArtworkCover, "2c26b46b68ff").ShouldBe($"/api/tracks/{id}/artwork/cover?v=2c26b46b68ff");
        ApiRoutes.TrackArtworkUpload(id).ShouldBe($"/api/tracks/{id}/artwork");
        ApiRoutes.BandInfo(id).ShouldBe($"/api/tracks/{id}/band-info");
        ApiRoutes.MetadataLookup(id).ShouldBe($"/api/tracks/{id}/metadata/lookup");
        var paths = OpenApiContract.Backend.Root["paths"]!.AsObject();
        foreach (var template in new[] { "/api/tracks/{trackId}/artwork/{variant}", "/api/tracks/{trackId}/artwork", "/api/tracks/{trackId}/band-info", "/api/tracks/{trackId}/metadata/lookup" })
        {
            paths.ContainsKey(template).ShouldBeTrue(template);
        }

        Strings(Schema("ArtworkVariant")["enum"]!).ShouldBe([ApiRoutes.ArtworkCover, ApiRoutes.ArtworkThumb]);
    }

    [Fact]
    public void Limits_MatchTheContract()
    {
        var lookup = Schema("MetadataLookupRequest")["properties"]!["title"]!;

        ContractLimits.MaxArtworkUploadBytes.ShouldBe(10_485_760);
        ContractLimits.ArtworkMediaTypes.ShouldBe(["image/jpeg", "image/png", "image/webp"]);
        lookup["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.MetadataNameMaxLength);
    }

    [Theory]
    [InlineData("queued-job", "QueuedJob")]
    [InlineData("separating-job", "SeparatingJob")]
    [InlineData("completed-job", "CompletedJob")]
    [InlineData("failed-job", "FailedJob")]
    [InlineData("metadata-queued-job", "QueuedMetadataJob")]
    [InlineData("metadata-running-job", "RunningMetadataJob")]
    [InlineData("metadata-completed-job", "CompletedMetadataJob")]
    [InlineData("metadata-failed-job", "FailedMetadataJob")]
    [InlineData("metadata-result-found", "FoundMetadataResult")]
    [InlineData("metadata-result-not-found", "NotFoundMetadataResult")]
    [InlineData("metadata-result-disabled", "DisabledMetadataResult")]
    public void WorkerExamples_AreCopiesOfTheWorkerContractExamples(string file, string example)
    {
        IsCopyOf(file, WorkerExample(example)).ShouldBeTrue(file);
    }

    [Fact]
    public void WorkerInlineExamples_AreCopies()
    {
        var paths = WorkerContract.Root["paths"]!;
        var result = paths["/v1/jobs/{jobId}/result"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["example"];
        var upload = paths["/v1/artwork"]!["post"]!["responses"]!["201"]!["content"]!["application/json"]!["example"];

        IsCopyOf("job-result", result).ShouldBeTrue();
        IsCopyOf("artwork-upload", upload).ShouldBeTrue();
    }
}
