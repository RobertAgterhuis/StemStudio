using System.Text.Json;
using StemStudio.Agent.Tools;

namespace StemStudio.Agent.Tests.Tools;

/// <summary>T014: argument validation against the tool schema, canonical JSON and the SHA-256 binding hash (FR-008, FR-015).</summary>
public sealed class ArgumentValidatorTests
{
    private const string TrackId = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static readonly ToolSchema Channel = ToolSchema.Of(
            Param.Choice("stem", "vocals", "drums", "bass"),
            Param.Number("gainDb", -60, 12).Optional(),
            Param.Number("gainChangeDb", -24, 24).Optional(),
            Param.Flag("mute").Optional())
        .AtMostOne("gainDb", "gainChangeDb");

    private static readonly ToolSchema Rich = ToolSchema.Of(
            Param.Uuid("trackId").Optional(),
            Param.Text("jobId", 1, 40).Optional(),
            Param.Text("name", 1, 5).Optional(),
            Param.Whole("takes", 1, 2).Optional(),
            new ToolParameter { Name = "topicId", Type = ParameterType.Text, Pattern = "^[a-z0-9-]+$", Required = false },
            Param.List("stems", Param.Choice("x", "vocals", "drums"), 1, 2).Optional(),
            Param.List("steps", Param.Record("step", Param.Text("title", 1, 10), Param.Text("tool", 1, 30).Optional()), 1, 3).Optional())
        .AtLeastOne("trackId", "jobId");

    private static ArgumentValidation Validate(ToolSchema schema, string json) => ToolArgumentValidator.Validate(schema, json);

    [Fact]
    public void ValidArguments_AreAcceptedAndCanonicalised()
    {
        var result = Validate(Channel, """{ "mute": true, "stem": "drums" }""");

        result.IsValid.ShouldBeTrue();
        result.Error.ShouldBeNull();
        result.Canonical.ShouldBe("""{"mute":true,"stem":"drums"}""");
        result.Hash.ShouldBe(CanonicalJson.Hash(result.Canonical));
        result.Arguments.GetProperty("stem").GetString().ShouldBe("drums");
    }

    [Theory]
    [InlineData("""{"stem":"drums","mute":"yes"}""", "mute")]
    [InlineData("""{"stem":"flute"}""", "stem")]
    [InlineData("""{"stem":"drums","gainDb":13}""", "gainDb")]
    [InlineData("""{"stem":"drums","gainDb":-61}""", "gainDb")]
    [InlineData("""{"stem":"drums","gainDb":"3"}""", "gainDb")]
    [InlineData("""{"stem":"drums","volume":1}""", "volume")]
    [InlineData("""{"mute":true}""", "stem")]
    [InlineData("""{"stem":"drums","gainDb":3,"gainChangeDb":3}""", "gainChangeDb")]
    [InlineData("""{"stem":1}""", "stem")]
    public void InvalidArguments_AreRejectedWithTheArgumentName(string json, string argument)
    {
        var result = Validate(Channel, json);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain(argument);
    }

    [Theory]
    [InlineData("""{"trackId":"not-a-guid"}""", "trackId")]
    [InlineData("""{"jobId":""}""", "jobId")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","name":"toolong"}""", "name")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","takes":1.5}""", "takes")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","takes":3}""", "takes")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","topicId":"Bad Topic"}""", "topicId")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","stems":[]}""", "stems")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","stems":["vocals","drums","vocals"]}""", "stems")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","stems":["flute"]}""", "stems")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","stems":"vocals"}""", "stems")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","steps":[{"tool":"x"}]}""", "title")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","steps":[{"title":"a","extra":1}]}""", "extra")]
    [InlineData("""{"trackId":"0f8fad5b-d9cb-469f-a165-70867728950e","steps":["a"]}""", "steps")]
    [InlineData("""{"name":"a"}""", "trackId")]
    public void RichSchema_RejectsEveryRuleViolation(string json, string argument)
    {
        var result = Validate(Rich, json);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain(argument);
    }

    [Fact]
    public void RichSchema_AcceptsNestedObjectsArraysAndIntegers()
    {
        var json = $$"""{"trackId":"{{TrackId}}","takes":2,"topicId":"agent-panel","stems":["drums"],"steps":[{"title":"Mute"}]}""";

        Validate(Rich, json).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    public void MissingArguments_MeanAnEmptyObject(string json)
    {
        var result = Validate(ToolSchema.Empty, json);

        result.IsValid.ShouldBeTrue();
        result.Canonical.ShouldBe("{}");
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("{not json")]
    [InlineData("\"text\"")]
    public void NonObjectsOrBrokenJson_AreRejected(string json)
    {
        var result = Validate(ToolSchema.Empty, json);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("object");
    }

    [Fact]
    public void ArgumentsAbove8Kb_AreRejected()
    {
        var json = JsonSerializer.Serialize(new { trackId = TrackId, jobId = new string('x', 9000) });

        var result = Validate(Rich, json);

        result.IsValid.ShouldBeFalse();
        result.Error!.ShouldContain("8 KB");
    }

    [Fact]
    public void Validate_RejectsANullSchema()
    {
        Should.Throw<ArgumentNullException>(() => ToolArgumentValidator.Validate(null!, "{}"));
    }

    [Fact]
    public void CanonicalJson_IsStableAcrossKeyOrderWhitespaceAndEscaping()
    {
        var a = CanonicalJson.Canonicalize("""{"b":{"y":1,"x":[2,{"q":"é","p":null}]},"a":"A"}""");
        var b = CanonicalJson.Canonicalize("""{ "a" : "A", "b": { "x": [2, {"p": null, "q": "é"}], "y": 1 } }""");

        a.ShouldBe(b);
        a.ShouldBe("""{"a":"A","b":{"x":[2,{"p":null,"q":"é"}],"y":1}}""");
        CanonicalJson.Hash(a).ShouldBe(CanonicalJson.Hash(b));
        CanonicalJson.Hash(a).Length.ShouldBe(64);
        CanonicalJson.Hash(a).ShouldMatch("^[0-9a-f]{64}$");
    }

    [Fact]
    public void CanonicalJson_KeepsBooleansNumbersAndArrays()
    {
        CanonicalJson.Canonicalize("[true,false,1.50,-2]").ShouldBe("[true,false,1.50,-2]");
        CanonicalJson.Hash("{}").ShouldNotBe(CanonicalJson.Hash("{\"a\":1}"));
    }

    [Fact]
    public void JsonSchema_IsAClosedObjectWithRequiredNamesAndRanges()
    {
        var schema = Channel.ToJsonSchema();

        schema.GetProperty("type").GetString().ShouldBe("object");
        schema.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
        schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ShouldBe(["stem"]);
        var gain = schema.GetProperty("properties").GetProperty("gainDb");
        gain.GetProperty("type").GetString().ShouldBe("number");
        gain.GetProperty("minimum").GetInt32().ShouldBe(-60);
        schema.GetProperty("properties").GetProperty("stem").GetProperty("enum").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public void JsonSchema_DescribesNestedTypesFormatsAndPatterns()
    {
        var properties = Rich.ToJsonSchema().GetProperty("properties");

        properties.GetProperty("trackId").GetProperty("format").GetString().ShouldBe("uuid");
        properties.GetProperty("takes").GetProperty("type").GetString().ShouldBe("integer");
        properties.GetProperty("topicId").GetProperty("pattern").GetString().ShouldBe("^[a-z0-9-]+$");
        properties.GetProperty("name").GetProperty("maxLength").GetInt32().ShouldBe(5);
        var steps = properties.GetProperty("steps");
        steps.GetProperty("type").GetString().ShouldBe("array");
        steps.GetProperty("maxItems").GetInt32().ShouldBe(3);
        var item = steps.GetProperty("items");
        item.GetProperty("type").GetString().ShouldBe("object");
        item.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
        item.GetProperty("required").EnumerateArray().Select(name => name.GetString()).ShouldBe(["title"]);
        properties.GetProperty("stems").GetProperty("items").GetProperty("type").GetString().ShouldBe("string");
    }

    [Fact]
    public void JsonSchema_KeepsFractionalRangesAndDescriptions()
    {
        var schema = ToolSchema.Of(Param.Number("pan", -1, 1).Describe("Pan position"), Param.Number("volume", 0, 0.5), Param.Flag("on"));

        var properties = schema.ToJsonSchema().GetProperty("properties");

        properties.GetProperty("pan").GetProperty("description").GetString().ShouldBe("Pan position");
        properties.GetProperty("volume").GetProperty("maximum").GetDouble().ShouldBe(0.5);
        properties.GetProperty("on").GetProperty("type").GetString().ShouldBe("boolean");
    }
}
