using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;

namespace StemStudio.Domain.Tests.Assets;

/// <summary>Feature 007 T025: library items (data-model 4.1-4.5).</summary>
public sealed class AssetTests
{
    public const string JobId = "a1b2c3d4e5f60718293a4b5c6d7e8f90";
    public static readonly DateTime T0 = new(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc);
    public static readonly Guid TrackId = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

    public static Provenance Provenance() => new()
    {
        Origin = AssetOrigin.Export,
        TrackId = TrackId,
        TrackTitle = "Never Gonna Give You Up",
        TrackArtist = "Rick Astley",
        Contents = "drums, raw",
        Recipe = new ExportRecipe
        {
            TrackId = TrackId,
            Mode = ExportMode.Raw,
            Channels = ["drums"],
            Format = new ExportFormat(AudioFormat.Flac, 16, null),
        },
    };

    public static Asset NewAsset(string name = "Song - drums") => Asset.Create(Guid.NewGuid(), name, Provenance(), T0);

    public static AssetFile AudioFile(long size = 1000) => new("audio", "audio.flac", "audio/flac", size, new string('a', 64));

    public static AudioFacts Facts() => new(AudioFormat.Flac, 16, null, 213.04, -0.3, 0);

    [Fact]
    public void Create_StartsQueuedWithoutFiles()
    {
        var asset = NewAsset();

        (asset.Kind, asset.Status, asset.Progress, asset.Error, asset.WorkerJobId).ShouldBe((AssetKind.Audio, AssetStatus.Queued, 0, null, null));
        asset.Files.ShouldBeEmpty();
        asset.Audio.ShouldBeNull();
        (asset.SizeBytes, asset.TrackId, asset.CreatedAt, asset.UpdatedAt).ShouldBe((0L, TrackId, T0, T0));
        asset.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("  Drums  ", "Drums")]
    [InlineData("Ünïcode ✓ name", "Ünïcode ✓ name")]
    public void Create_TrimsTheName(string name, string stored)
    {
        NewAsset(name).Name.ShouldBe(stored);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tab\tname")]
    [InlineData("line\nbreak")]
    public void Names_OutsideTheRulesAreRejected(string name)
    {
        Should.Throw<DomainValidationException>(() => NewAsset(name)).Errors.Single().Field.ShouldBe("name");
        Should.Throw<DomainValidationException>(() => NewAsset().Rename(name, T0));
    }

    [Fact]
    public void Names_LongerThan120AreRejected()
    {
        Should.Throw<DomainValidationException>(() => NewAsset(new string('n', 121)));
        NewAsset(new string('n', 120)).Name.Length.ShouldBe(120);
    }

    [Fact]
    public void Rename_ReplacesTheName()
    {
        var asset = NewAsset();

        asset.Rename(" Drums for practice ", T0.AddMinutes(1));

        (asset.Name, asset.UpdatedAt).ShouldBe(("Drums for practice", T0.AddMinutes(1)));
    }

    [Fact]
    public void Progress_MovesToRenderingClampsAndNeverDecreases()
    {
        var asset = Submitted();

        asset.ApplyProgress(0.624, T0.AddSeconds(1));
        (asset.Status, asset.Progress).ShouldBe((AssetStatus.Rendering, 62));
        asset.ApplyProgress(0.1, T0.AddSeconds(2));
        asset.Progress.ShouldBe(62);
        asset.ApplyProgress(7, T0.AddSeconds(3));
        asset.Progress.ShouldBe(100);
    }

    [Fact]
    public void Progress_IsIgnoredOnceFinished()
    {
        var asset = Submitted();
        asset.Fail("boom", T0);

        asset.ApplyProgress(0.5, T0.AddSeconds(1));

        (asset.Status, asset.Progress).ShouldBe((AssetStatus.Failed, 0));
    }

    [Fact]
    public void AssignJob_RequiresAnActiveItemAndAJobId()
    {
        var asset = NewAsset();

        Should.Throw<ArgumentException>(() => asset.AssignJob("nope", T0));
        asset.AssignJob(JobId, T0);
        asset.WorkerJobId.ShouldBe(JobId);
        asset.Fail("x", T0);
        Should.Throw<InvalidOperationException>(() => asset.AssignJob(JobId, T0));
    }

    [Fact]
    public void Complete_SetsFilesAudioAndSize()
    {
        var asset = Submitted();

        asset.Complete([AudioFile(1234)], Facts(), T0.AddSeconds(9));

        (asset.Status, asset.Progress, asset.SizeBytes, asset.WorkerJobId).ShouldBe((AssetStatus.Ready, 100, 1234L, null));
        asset.Files.Single().Role.ShouldBe("audio");
        asset.Audio.ShouldBe(Facts());
        asset.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Complete_RequiresAnActiveItemAndFiles()
    {
        Should.Throw<ArgumentException>(() => Submitted().Complete([], Facts(), T0));
        var failed = Submitted();
        failed.Fail("x", T0);
        Should.Throw<InvalidOperationException>(() => failed.Complete([AudioFile()], Facts(), T0));
    }

    [Fact]
    public void Fail_KeepsAReadableReason()
    {
        var blank = Submitted();
        var longer = Submitted();

        blank.Fail(" ", T0);
        longer.Fail(new string('e', 700), T0);

        (blank.Status, blank.Error).ShouldBe((AssetStatus.Failed, Asset.RenderFailedDefault));
        longer.Error!.Length.ShouldBe(Asset.MaxErrorLength);
        Should.Throw<InvalidOperationException>(() => blank.Fail("again", T0));
    }

    [Fact]
    public void Retry_OnlyFromFailedStartsOver()
    {
        var asset = Submitted();
        asset.ApplyProgress(0.5, T0);
        Should.Throw<InvalidOperationException>(() => asset.Retry(T0));
        asset.Fail("interrupted by restart", T0);

        asset.Retry(T0.AddMinutes(1));

        (asset.Status, asset.Progress, asset.Error, asset.WorkerJobId, asset.UpdatedAt).ShouldBe((AssetStatus.Queued, 0, null, null, T0.AddMinutes(1)));
    }

    [Fact]
    public void Create_RejectsAProvenanceWithoutTitle()
    {
        Should.Throw<ArgumentException>(() => Asset.Create(Guid.NewGuid(), "x", Provenance() with { TrackTitle = " " }, T0));
    }

    [Theory]
    [InlineData("Never Gonna Give You Up", "drums", new string[0], "Never Gonna Give You Up - drums")]
    [InlineData("Song", "drums", new[] { "Song - drums" }, "Song - drums (2)")]
    [InlineData("Song", "drums", new[] { "song - DRUMS", "Song - drums (2)" }, "Song - drums (3)")]
    public void DefaultName_IsTitleAndContentsMadeUnique(string title, string contents, string[] taken, string expected)
    {
        AssetNaming.DefaultName(title, contents, taken).ShouldBe(expected);
    }

    [Fact]
    public void DefaultName_IsCutTo120CharactersIncludingTheSuffix()
    {
        var title = new string('t', 200);

        var first = AssetNaming.DefaultName(title, "drums", []);
        var second = AssetNaming.DefaultName(title, "drums", [first]);

        first.Length.ShouldBe(120);
        second.Length.ShouldBe(120);
        second.ShouldEndWith(" (2)");
    }

    [Theory]
    [InlineData("Song - drums", "flac", "Song - drums.flac")]
    [InlineData("a<b>c:d\"e/f\\g|h?i*j", "wav", "a_b_c_d_e_f_g_h_i_j.wav")]
    [InlineData("  tab\there ", "mp3", "tab_here.mp3")]
    public void DownloadFileName_ReplacesForbiddenCharacters(string name, string extension, string expected)
    {
        AssetNaming.DownloadFileName(name, extension).ShouldBe(expected);
    }

    [Fact]
    public void DownloadFileName_IsCutTo150CharactersBeforeTheExtension()
    {
        AssetNaming.DownloadFileName(new string('n', 200), "flac").ShouldBe(new string('n', 150) + ".flac");
    }

    private static Asset Submitted()
    {
        var asset = NewAsset();
        asset.AssignJob(JobId, T0);
        return asset;
    }
}
