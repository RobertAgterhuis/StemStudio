using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Tests.Scores;

/// <summary>Shared data and helpers of the feature 008 score tests.</summary>
internal static class ScoreTestData
{
    public const string JobId = "c4e6a8b0d2f4416385a7c9e1b3d5f7a9";

    public static readonly string[] Roles = ["pdf", "musicxml", "midi"];

    /// <summary>The bytes the fake worker serves for a role.</summary>
    public static byte[] FileBytes(string role) => Encoding.UTF8.GetBytes($"{role}:{JobId}");

    public static string Sha(string role) => Convert.ToHexStringLower(SHA256.HashData(FileBytes(role)));

    /// <summary>A completed track with drums split into the five pieces and vocals into lead/backing.</summary>
    public static Track DrumsSplitTrack()
    {
        var track = AssetTestData.NamedTrack();
        track.RequestDetail(SyncTestData.T0);
        track.AssignDetailJob(SyncTestData.DetailJobId, SyncTestData.T0);
        track.CompleteDetail(
        [
            new ParentOutcome("drums", ParentOutcomeKind.Split, [new("kick", 8), new("snare", 8), new("toms", 8), new("hihat", 8), new("cymbals", 8), new("rest", 8)]),
            new ParentOutcome("vocals", ParentOutcomeKind.Split, [new("lead", 8), new("backing", 8), new("rest", 8)]),
            new ParentOutcome("bass", ParentOutcomeKind.NotSupported, []),
            new ParentOutcome("guitar", ParentOutcomeKind.NothingFurther, []),
            new ParentOutcome("piano", ParentOutcomeKind.SkippedSilent, []),
            new ParentOutcome("other", ParentOutcomeKind.NotReliable, []),
        ],
            SyncTestData.T0);
        return track;
    }

    public static CreateScoreRequest Request(Guid trackId, string channel) => new() { Source = AudioSourceDto.FromChannel(trackId, channel) };

    public static Task<HttpResponseMessage> PostScoreAsync(this HttpClient client, CreateScoreRequest request) =>
        client.PostAsJsonAsync(new Uri(ApiRoutes.Scores, UriKind.Relative), request, TestContext.Current.CancellationToken);

    public static WorkerScoreSummary Summary(ScoreProfile profile = ScoreProfile.Bass, ScoreEngine engine = ScoreEngine.BasicPitch) => new()
    {
        Profile = profile,
        Engine = engine,
        Key = new KeyInfo("A-", KeyMode.Major, 0.83, KeySource.Estimated),
        Bpm = 118,
        TimeSignature = "4/4",
        Bars = 125,
        Notes = 386,
        Pages = 3,
        Resolution = ScoreResolution.Sixteenth,
        Warnings = [new ScoreWarning(ScoreWarningCode.NotesCleaned, "87 very short or out-of-range notes were removed.")],
    };

    public static WorkerTranscriptionResult Result(WorkerScoreSummary? summary = null, string? pdfSha = null) => new(
        [
            new WorkerTranscriptionFile("pdf", "score.pdf", "application/pdf", FileBytes("pdf").Length, pdfSha ?? Sha("pdf")),
            new WorkerTranscriptionFile("musicxml", "score.musicxml", "application/vnd.recordare.musicxml+xml", FileBytes("musicxml").Length, Sha("musicxml")),
            new WorkerTranscriptionFile("midi", "score.mid", "audio/midi", FileBytes("midi").Length, Sha("midi")),
        ],
        summary ?? Summary());

    /// <summary>The worker reports every transcription job completed with <paramref name="result"/>.</summary>
    public static void CompleteTranscriptions(FakeWorkerClient worker, WorkerTranscriptionResult? result = null, WorkerJobDevice? device = null)
    {
        worker.OnGetTranscription = id => Task.FromResult<WorkerTranscriptionJob?>(
            FakeWorkerClient.TranscriptionJob(id, WorkerRenderJobState.Completed, 1, result ?? Result()) with { Device = device ?? new WorkerJobDevice(ProcessingDeviceKind.Cpu, null, false, null) });
        worker.OnDownloadTranscriptionFile = (_, role, stream) => stream.WriteAsync(FileBytes(role)).AsTask();
    }

    /// <summary>The worker creates transcription jobs with the id <see cref="JobId"/>.</summary>
    public static void FixedJobId(FakeWorkerClient worker) =>
        worker.OnCreateTranscription = _ => Task.FromResult(FakeWorkerClient.TranscriptionJob(JobId, WorkerRenderJobState.Queued));

    public static WorkerHealth Health(ProcessingDeviceKind kind, bool pianoReady = false) =>
        FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = kind, Name = kind == ProcessingDeviceKind.Gpu ? "NVIDIA GeForce RTX 5070 Ti" : null }) with
        {
            Transcription = new WorkerTranscriptionHealth(new Dictionary<string, string>
            {
                ["basic-pitch-icassp2022"] = "ready",
                ["piano-crnn-note-pedal"] = pianoReady ? "ready" : "not-downloaded",
            }),
        };
}
