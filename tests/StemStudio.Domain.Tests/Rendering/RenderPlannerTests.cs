using System.Text.Json;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Tests.Tracks;

namespace StemStudio.Domain.Tests.Rendering;

/// <summary>Feature 007 T027: the render planner against the shared <c>render-vectors.json</c> 1.0.0 (FR-002, FR-003, SC-003).</summary>
public sealed class RenderPlannerTests
{
    private static readonly Lazy<JsonElement> Vectors = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Rendering", "render-vectors.json"))).RootElement.Clone());

    private static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    public static TheoryData<string> GateCases()
    {
        var data = new TheoryData<string>();
        foreach (var gate in Vectors.Value.GetProperty("gate").EnumerateArray())
        {
            data.Add(gate.GetProperty("name").GetString()!);
        }

        return data;
    }

    [Fact]
    public void Vectors_AreVersion100WithTwelveGateCases()
    {
        Vectors.Value.GetProperty("version").GetString().ShouldBe("1.0.0");
        GateCases().Count.ShouldBe(12);
    }

    [Theory]
    [MemberData(nameof(GateCases))]
    public void EveryGateCase_YieldsTheExpectedPlan(string name)
    {
        var gate = Vectors.Value.GetProperty("gate").EnumerateArray().Single(g => g.GetProperty("name").GetString() == name);
        var expected = gate.GetProperty("expected");

        var plan = () => RenderPlanner.Plan(Source(gate), Mode(gate), Channels(gate), Mix(gate.GetProperty("mix")));

        if (expected.TryGetProperty("error", out var error))
        {
            error.GetString().ShouldBe("nothing-to-export");
            Should.Throw<NothingToExportException>(plan).Message.ShouldBe(NothingToExportException.Text);
            return;
        }

        var result = plan();
        result.MasterGain.ShouldBe(expected.GetProperty("masterGain").GetDouble(), 1e-9);
        result.Contents.ShouldBe(expected.GetProperty("contents").GetString());
        result.Inputs.Select(Describe).ShouldBe(expected.GetProperty("inputs").EnumerateArray().Select(Describe));
    }

    [Fact]
    public void Source_OfATrackListsItsStemsAndSplitParents()
    {
        var source = RenderSource.Of(DetailTestData.DetailCompleted());

        source.Stems.ShouldBe(["vocals", "drums", "bass", "guitar", "piano", "other"]);
        source.Split.Keys.ShouldBe(["vocals", "drums"], ignoreOrder: true);
        source.Split["drums"].ShouldBe(["kick", "snare", "rest"]);
        RenderSource.Of(DetailTestData.CompletedTrack()).Split.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("piano", "channels[0]", "The track has no stem piano.")]
    [InlineData("drums.kick", "channels[0]", "The track has no detail stem drums.kick.")]
    [InlineData("Vocals", "channels[0]", "'Vocals' is not a channel id.")]
    [InlineData("bass.rest", "channels[0]", "'bass.rest' is not a channel id.")]
    public void UnknownChannels_AreValidationErrorsNamingTheEntry(string channel, string field, string message)
    {
        var error = Should.Throw<DomainValidationException>(() => RenderPlanner.Plan(FourStem(), ExportMode.Raw, [channel], null));

        error.Errors.ShouldBe([new DomainError(field, message)]);
    }

    [Fact]
    public void DuplicateChannels_NameTheRepeatedEntry()
    {
        var error = Should.Throw<DomainValidationException>(() => RenderPlanner.Plan(FourStem(), ExportMode.Raw, ["bass", "vocals", "bass"], null));

        error.Errors.Single().Field.ShouldBe("channels[2]");
    }

    [Fact]
    public void MoreThan56Channels_AreRejected()
    {
        var channels = Enumerable.Repeat("bass", 57).ToList();

        Should.Throw<DomainValidationException>(() => RenderPlanner.Plan(FourStem(), ExportMode.Raw, channels, null)).Errors.Single().Field.ShouldBe("channels");
    }

    [Fact]
    public void RawWithoutChannels_AndMixWithoutMix_AreRejected()
    {
        Should.Throw<DomainValidationException>(() => RenderPlanner.Plan(FourStem(), ExportMode.Raw, null, null)).Errors.Single().Field.ShouldBe("channels");
        Should.Throw<DomainValidationException>(() => RenderPlanner.Plan(FourStem(), ExportMode.Mix, null, null)).Errors.Single().Field.ShouldBe("mix");
        Should.Throw<DomainValidationException>(() => RenderPlanner.Plan(FourStem(), ExportMode.Raw, [], null)).Errors.Single().Field.ShouldBe("channels");
    }

    [Fact]
    public void ManyRawChannels_AreCountedInTheContents()
    {
        var plan = RenderPlanner.Plan(SixStem(), ExportMode.Raw, ["vocals", "drums", "bass", "guitar", "piano"], null);

        plan.Contents.ShouldBe("5 channels, raw");
        plan.ChannelText.ShouldBe("5 channels");
    }

    [Theory]
    [InlineData(30, 75, "drums, raw, 0:30-1:15")]
    [InlineData(0, 605.5, "drums, raw, 0:00-10:05")]
    public void Contents_WithARangeAddsTheTimes(double start, double end, string expected)
    {
        ExportContents.WithRange("drums, raw", new ExportRange(start, end)).ShouldBe(expected);
        ExportContents.WithRange("drums, raw", null).ShouldBe("drums, raw");
    }

    [Fact]
    public void ExportMix_IsValidatedWithTheMixRules()
    {
        var track = DetailTestData.DetailCompleted();
        var mix = new ExportMix(2, [new ChannelSettings { Stem = "harp" }])
        {
            DetailChannels = [new DetailChannelSettings { Parent = "drums", Stem = "toms" }],
        };

        var errors = RenderPlanner.ValidateMix(mix, track);

        errors.Select(e => e.Field).ShouldBe(["masterVolume", "channels[0].stem", "detailChannels[0].stem"], ignoreOrder: true);
        RenderPlanner.ValidateMix(new ExportMix(1, []), track).ShouldBeEmpty();
    }

    private static string Describe(PlannedInput input) =>
        FormattableString.Invariant($"{input.ChannelId}|{input.Path}|{input.Gain:0.######}|{input.Pan:0.######}");

    private static string Describe(JsonElement input) => FormattableString.Invariant(
        $"{input.GetProperty("channelId").GetString()}|{input.GetProperty("path").GetString()}|{input.GetProperty("gain").GetDouble():0.######}|{input.GetProperty("pan").GetDouble():0.######}");

    private static RenderSource FourStem() => new(TrackId, ["vocals", "drums", "bass", "other"], new Dictionary<string, IReadOnlyList<string>>());

    private static RenderSource SixStem() => new(TrackId, ["vocals", "drums", "bass", "guitar", "piano", "other"], new Dictionary<string, IReadOnlyList<string>>());

    private static RenderSource Source(JsonElement gate)
    {
        var track = gate.GetProperty("track");
        var split = track.GetProperty("split").EnumerateObject()
            .ToDictionary(parent => parent.Name, parent => (IReadOnlyList<string>)parent.Value.EnumerateArray().Select(n => n.GetString()!).ToList());
        return new RenderSource(Guid.Parse(gate.GetProperty("trackId").GetString()!), Strings(track.GetProperty("stems")), split);
    }

    private static ExportMode Mode(JsonElement gate) => gate.GetProperty("mode").GetString() == "raw" ? ExportMode.Raw : ExportMode.Mix;

    private static List<string>? Channels(JsonElement gate) =>
        gate.GetProperty("channels").ValueKind == JsonValueKind.Null ? null : Strings(gate.GetProperty("channels"));

    private static ExportMix Mix(JsonElement mix) => new(
        mix.GetProperty("masterVolume").GetDouble(),
        mix.GetProperty("channels").EnumerateArray().Select(c => Fader(new ChannelSettings { Stem = c.GetProperty("stem").GetString()! }, c)).ToList())
    {
        Expanded = Strings(mix.GetProperty("expanded")),
        DetailChannels = mix.GetProperty("detailChannels").EnumerateArray()
            .Select(c => Fader(new DetailChannelSettings { Parent = c.GetProperty("parent").GetString()!, Stem = c.GetProperty("stem").GetString()! }, c)).ToList(),
    };

    private static T Fader<T>(T settings, JsonElement json)
        where T : FaderSettings => settings with
        {
            Volume = json.GetProperty("volume").GetDouble(),
            Pan = json.GetProperty("pan").GetDouble(),
            Mute = json.GetProperty("mute").GetBoolean(),
            Solo = json.GetProperty("solo").GetBoolean(),
        };

    private static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(n => n.GetString()!).ToList();
}
