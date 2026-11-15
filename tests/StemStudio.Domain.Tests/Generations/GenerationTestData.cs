using StemStudio.Domain.Generations;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tests.Styles;

namespace StemStudio.Domain.Tests.Generations;

internal static class GenerationTestData
{
    public const string JobId = "5b0f7e2c9a1d4c3e8f6a2b1d0c9e8f7a";

    public static readonly DateTime T0 = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    public static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    public static RestyleDetails Details() => new()
    {
        SourceTrackId = TrackId,
        SourceTrackTitle = "Never Gonna Give You Up",
        Range = new SourceRange(60, 105, RestyleMode.Range),
        IncludedStems = ["vocals", "drums", "bass", "guitar", "piano", "other"],
        Style = StyleTestData.Preset().Snapshot(),
        Strength = Strength.Balanced,
        Caption = "Jazz swing, swing feel, piano, upright bass, brushed drums, relaxed, warm",
        AudioCoverStrength = 0.55,
    };

    public static Generation Queued(int takes = 2) =>
        Generation.CreateRestyle(Guid.NewGuid(), Details(), Enumerable.Range(1, takes).Select(i => i * 1000).ToList(), T0);

    public static GenerationTake Take(int index, int seed = 1000) =>
        new(index, seed, 45, new string('a', 64), 9_000_000, -0.9, $"generations/x/take-{index}.flac", new TakeModel("acestep-v15-turbo", "19671f4"));

    public static Generation Completed()
    {
        var generation = Queued();
        generation.AssignJob(JobId, T0);
        generation.Complete([Take(1, 1000), Take(2, 2000)], T0.AddSeconds(40));
        return generation;
    }
}
