using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Tools;

namespace StemStudio.Agent.Tests.Tools;

/// <summary>T014: result projections (whitelist safety net, 4 KB cap with marker, taint via untrusted_text; FR-013, FR-026).</summary>
public sealed class ResultProjectorTests
{
    private static int Bytes(string json) => Encoding.UTF8.GetByteCount(json);

    [Fact]
    public void SmallTrustedProjection_IsKeptAsIs()
    {
        var result = ResultProjector.Project(new JsonObject { ["deleted"] = true });

        result.Json.ShouldBe("""{"deleted":true}""");
        result.Tainted.ShouldBeFalse();
        result.Truncated.ShouldBeFalse();
    }

    [Fact]
    public void UntrustedText_TaintsTheRun_AtAnyDepth()
    {
        var projection = new JsonArray(new JsonObject { ["trackId"] = "t", [ResultProjector.UntrustedField] = "Song - Evil" });

        var result = ResultProjector.Project(projection);

        result.Tainted.ShouldBeTrue();
        result.Json.ShouldContain("untrusted_text");
    }

    [Fact]
    public void EmptyUntrustedText_DoesNotTaint()
    {
        ResultProjector.Project(new JsonObject { [ResultProjector.UntrustedField] = string.Empty }).Tainted.ShouldBeFalse();
        ResultProjector.Project(new JsonObject { [ResultProjector.UntrustedField] = null }).Tainted.ShouldBeFalse();
    }

    [Theory]
    [InlineData("lyrics")]
    [InlineData("lyricsText")]
    [InlineData("text")]
    [InlineData("lines")]
    [InlineData("plainText")]
    [InlineData("biography")]
    [InlineData("extract")]
    [InlineData("path")]
    [InlineData("filePath")]
    [InlineData("url")]
    [InlineData("audioUrl")]
    [InlineData("vocalAudioUrl")]
    [InlineData("sampleUrl")]
    [InlineData("pageUrl")]
    [InlineData("sourceUrl")]
    [InlineData("personName")]
    public void ForbiddenFields_AreNeverProjected(string field)
    {
        var projection = new JsonObject
        {
            ["ok"] = true,
            [field] = "secret",
            ["nested"] = new JsonArray(new JsonObject { [field] = "secret", ["keep"] = 1 }),
        };

        var result = ResultProjector.Project(projection);

        result.Json.ShouldNotContain("secret");
        result.Json.ShouldContain("keep");
    }

    [Theory]
    [InlineData("lines")]
    [InlineData("path")]
    public void ForbiddenNames_KeepListsOfRows_ButStripTheirTexts(string field)
    {
        var projection = new JsonObject
        {
            [field] = new JsonArray(new JsonObject { ["line"] = 1, ["syllables"] = 8, ["text"] = "secret" }),
            ["other"] = new JsonObject { [field] = new JsonArray("secret line", "more") },
            ["mixed"] = new JsonObject { [field] = new JsonArray(new JsonObject { ["a"] = 1 }, "secret") },
        };

        var result = ResultProjector.Project(projection);

        result.Json.ShouldBe($$$"""{"{{{field}}}":[{"line":1,"syllables":8}],"other":{},"mixed":{}}""");
    }

    [Fact]
    public void LargeArray_IsCutTo4KbWithAMarker()
    {
        var projection = new JsonArray([.. Enumerable.Range(0, 200).Select(i => (JsonNode)new JsonObject { ["trackId"] = $"track-{i:000}", ["status"] = "completed" })]);

        var result = ResultProjector.Project(projection);

        Bytes(result.Json).ShouldBeLessThanOrEqualTo(ResultProjector.MaxBytes);
        result.Truncated.ShouldBeTrue();
        var items = JsonNode.Parse(result.Json)!.AsArray();
        items[^1]!["truncated"]!.GetValue<bool>().ShouldBeTrue();
        items[0]!["trackId"]!.GetValue<string>().ShouldBe("track-000");
    }

    [Fact]
    public void LargeObject_ShortensItsArraysAndStrings_AndMarksTruncation()
    {
        var projection = new JsonObject
        {
            ["status"] = "found",
            ["members"] = new JsonArray([.. Enumerable.Range(0, 100).Select(i => (JsonNode)JsonValue.Create($"Member number {i}"))]),
            [ResultProjector.UntrustedField] = new string('w', 6000),
        };

        var result = ResultProjector.Project(projection);

        Bytes(result.Json).ShouldBeLessThanOrEqualTo(ResultProjector.MaxBytes);
        var node = JsonNode.Parse(result.Json)!.AsObject();
        node["truncated"]!.GetValue<bool>().ShouldBeTrue();
        node["status"]!.GetValue<string>().ShouldBe("found");
        result.Tainted.ShouldBeTrue();
    }

    [Fact]
    public void HugeScalar_IsShortened()
    {
        var result = ResultProjector.Project(JsonValue.Create(new string('z', 10_000)));

        Bytes(result.Json).ShouldBeLessThanOrEqualTo(ResultProjector.MaxBytes);
        result.Truncated.ShouldBeTrue();
    }

    [Fact]
    public void NullProjection_IsAnEmptyObject()
    {
        ResultProjector.Project(null).Json.ShouldBe("{}");
    }

    [Fact]
    public void Error_IsAReadableObjectWithinTheLimit()
    {
        var result = ResultProjector.Error(new string('e', 900));

        var node = JsonNode.Parse(result.Json)!.AsObject();
        node["ok"]!.GetValue<bool>().ShouldBeFalse();
        node["error"]!.GetValue<string>().Length.ShouldBeLessThanOrEqualTo(500);
        result.Tainted.ShouldBeFalse();
    }

    [Fact]
    public void Untrusted_CleansControlCharactersAndCapsLength()
    {
        ResultProjector.Untrusted("a\u0000b\nc\u001b[31m", 100).ShouldBe("ab c[31m");
        ResultProjector.Untrusted(new string('x', 900), 500)!.Length.ShouldBe(500);
        ResultProjector.Untrusted(null, 10).ShouldBeNull();
        ResultProjector.Untrusted("   ", 10).ShouldBeNull();
    }

    [Fact]
    public void Join_CombinesTitleAndArtist()
    {
        ResultProjector.Join("Jolene", "Dolly Parton").ShouldBe("Jolene - Dolly Parton");
        ResultProjector.Join("Broken Upload", null).ShouldBe("Broken Upload");
    }

    [Fact]
    public void ProjectionJson_IsValidJson()
    {
        var result = ResultProjector.Project(new JsonObject { ["name"] = "Quote \" and é" });

        JsonDocument.Parse(result.Json).RootElement.GetProperty("name").GetString().ShouldBe("Quote \" and é");
    }
}
