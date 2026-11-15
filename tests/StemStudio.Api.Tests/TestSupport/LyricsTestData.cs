using System.Text.Json;
using System.Text.Json.Serialization;
using StemStudio.Api.Worker;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>Feature 006 test data: the worker lyrics examples (self-written text) and tracks in every lyrics state.</summary>
internal static class LyricsTestData
{
    public const string LookupJobId = "5e8d2c4a6b1f40e9a7c3d5b1e9f20c48";
    public const string AlignJobId = "9a4c6e8b0d2f41a3b5c7d9e1f3a5b7c9";
    public const string StandardJobId = "0123456789abcdef0123456789abcdef";

    /// <summary>Every word of the example documents, for the "never logged" checks (FR-010, SC-006).</summary>
    public static readonly string[] FixtureWords = ["Fake line one", "La la la"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private static DateTime T0 => SyncTestData.T0;

    /// <summary>A worker result example (<c>lyrics-result-word</c>, <c>-line</c>, <c>-plain</c>, <c>-aligned</c>, <c>-instrumental</c>, <c>-not-found</c>, <c>-disabled</c>).</summary>
    public static WorkerLyricsResult Result(string example) => JsonSerializer.Deserialize<WorkerLyricsResult>(WorkerExamples.Read(example), Json)!;

    /// <summary>The document of a result example.</summary>
    public static LyricsDocument Document(string example) => Result(example).Lyrics!;

    /// <summary>A completed track whose 005 lookup names are set and whose lyrics are searching without a worker job.</summary>
    public static Track Searching(string videoId = "dQw4w9WgXcQ")
    {
        var track = SyncTestData.CompletedTrack(videoId);
        track.ScheduleLookup(new LookupQuery("Fake Song", "The Fixture Band", "Fixture Album", 262.0), T0);
        track.ScheduleLyricsLookup(T0);
        return track;
    }

    /// <summary>A track whose lyrics search has the worker job <see cref="LookupJobId"/>.</summary>
    public static Track SearchingWithJob(string videoId = "dQw4w9WgXcQ")
    {
        var track = Searching(videoId);
        track.Lyrics!.AssignJob(LookupJobId, T0);
        return track;
    }

    /// <summary>A track whose lyrics came from a lookup result example.</summary>
    public static Track WithResult(string example, string videoId = "dQw4w9WgXcQ")
    {
        var track = Searching(videoId);
        var result = Result(example);
        track.Lyrics!.ApplyLookup(Enum.Parse<LyricsOutcome>(result.Outcome.ToString()), result.Lyrics, T0);
        return track;
    }

    /// <summary>A track with found word-synced lyrics.</summary>
    public static Track Found(string videoId = "dQw4w9WgXcQ") => WithResult("lyrics-result-word", videoId);

    /// <summary>A track whose plain lyrics wait for the alignment (automatic rule still pending).</summary>
    public static Track AlignPending(string videoId = "dQw4w9WgXcQ") => WithResult("lyrics-result-plain", videoId);

    /// <summary>A track whose plain lyrics are aligned by the worker job <see cref="AlignJobId"/>.</summary>
    public static Track Aligning(string videoId = "dQw4w9WgXcQ")
    {
        var track = AlignPending(videoId);
        track.Lyrics!.StartAlign(T0);
        track.Lyrics.AssignJob(AlignJobId, T0);
        return track;
    }

    public static WorkerHealth Health(ProcessingDeviceKind kind, AlignAuto auto = AlignAuto.Gpu, bool modelCached = true) =>
        FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = kind, Name = kind == ProcessingDeviceKind.Gpu ? "NVIDIA GeForce RTX 5070 Ti" : null }) with
        {
            LyricsAlignment = new WorkerLyricsAlignmentHealth { Available = true, Model = "large-v3-turbo", ModelCached = modelCached, Auto = auto },
        };
}
