using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Styles;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Voices;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>
/// Builds a small real library in an <see cref="ApiFactory"/>: rows through the context and their files in the data
/// directory, with deterministic content, so backups, restores, the storage overview and the clean-up can be checked
/// byte by byte (013 SC-001, SC-006).
/// </summary>
internal sealed class LibraryFixture(ApiFactory factory)
{
    public static readonly DateTime T0 = ApiFactory.Start.UtcDateTime;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public string DataDir => factory.DataDir;

    /// <summary>A completed track with its six stems, a user cover and, optionally, completed detail stems.</summary>
    public async Task<Track> TrackAsync(string videoId = "dQw4w9WgXcQ", bool detail = true)
    {
        var track = detail ? SyncTestData.DetailCompleted(videoId) : SyncTestData.CompletedTrack(videoId);
        await AddAsync(track);
        foreach (var stem in track.Stems)
        {
            await FileAsync($"tracks/{Id(track.Id)}/stems/{stem.Name}.flac", 64);
        }

        await FileAsync($"tracks/{Id(track.Id)}/artwork/user/cover.jpg", 16);
        foreach (var parent in track.DetailPhase?.Parents ?? [])
        {
            foreach (var stem in parent.Stems)
            {
                await FileAsync($"tracks/{Id(track.Id)}/detail/{parent.Parent}/{stem.Name}.flac", 32);
            }
        }

        return track;
    }

    /// <summary>A queued upload track with its pending upload file.</summary>
    public async Task<Track> PendingUploadAsync()
    {
        var bytes = Encoding.UTF8.GetBytes("pending upload " + Guid.NewGuid());
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".mp3";
        var track = Track.FromUpload(Guid.NewGuid(), SourceKey.FromStored("upload:" + name[..64]), new UploadedFile("song.mp3", name), T0);
        await AddAsync(track);
        await WriteAsync("uploads/" + name, bytes);
        return track;
    }

    /// <summary>A ready audio export of the track.</summary>
    public async Task<Asset> ExportAsync(Track track)
    {
        var asset = Asset.Create(Guid.NewGuid(), "Drums " + Guid.NewGuid().ToString("N")[..6], new Provenance
        {
            Origin = AssetOrigin.Export,
            TrackId = track.Id,
            TrackTitle = track.Title,
            Contents = "drums, raw",
            Recipe = new ExportRecipe { TrackId = track.Id, Mode = ExportMode.Raw, Channels = ["drums"], Format = new ExportFormat(AudioFormat.Flac, 16, null) },
        }, T0);
        asset.AssignJob("0123456789abcdef0123456789abcde0", T0);
        var hash = await FileAsync($"assets/{Id(asset.Id)}/audio.flac", 48);
        asset.Complete([new AssetFile("audio", "audio.flac", "audio/flac", 48, hash)], new AudioFacts(AudioFormat.Flac, 16, null, 10, -1, 0), T0);
        await AddAsync(asset);
        return asset;
    }

    /// <summary>A completed restyle of the track with one take file.</summary>
    public async Task<Generation> GenerationAsync(Track track)
    {
        var style = Style.CreateUser(Guid.NewGuid(), new StyleFields
        {
            Name = "Rainy " + Guid.NewGuid().ToString("N")[..6],
            GenreFamily = GenreFamily.Jazz,
            TempoFeel = TempoFeel.Laidback,
            VocalHandling = VocalHandling.Instrumental,
            DefaultStrength = Strength.Light,
        }, T0);
        var details = new RestyleDetails
        {
            SourceTrackId = track.Id,
            SourceTrackTitle = track.Title,
            Range = new SourceRange(0, 30, RestyleMode.InPlace),
            IncludedStems = ["drums", "bass"],
            Style = style.Snapshot(),
            Strength = Strength.Light,
            Caption = "Jazz",
            AudioCoverStrength = 0.7,
        };
        var generation = Generation.CreateRestyle(Guid.NewGuid(), details, [11], T0);
        generation.AssignJob("0123456789abcdef0123456789abcde1", T0);
        var path = $"generations/{Id(generation.Id)}/take-1.flac";
        var hash = await FileAsync(path, 40);
        generation.Complete([new GenerationTake(1, 11, 30, hash, 40, -1.5, path, new TakeModel("acestep-v15-turbo", "19671f4"))], T0);
        await AddAsync(generation);
        return generation;
    }

    /// <summary>A completed voice conversion of a generation take with the profile.</summary>
    public async Task<Generation> ConversionAsync(Generation source, VoiceProfile profile)
    {
        var conversion = Generation.CreateVoiceConversion(Guid.NewGuid(), new VoiceConversionDetails
        {
            SourceGenerationId = source.Id,
            SourceTakeIndex = 1,
            VoiceProfileId = profile.Id,
            VoiceProfileName = profile.DisplayName,
        }, T0);
        await AddAsync(conversion);
        return conversion;
    }

    /// <summary>A voice profile with a valid consent record and its sample file.</summary>
    public async Task<VoiceProfile> VoiceProfileAsync(string name = "My voice")
    {
        var id = Guid.NewGuid();
        var path = $"voices/{Id(id)}/sample.flac";
        var hash = await FileAsync(path, 24);
        var profile = VoiceProfile.Create(id, new VoiceProfileDraft(
            name,
            new VoiceConsent(ConsentStatement.Version, ConsentStatement.Sha256, "Pat Example", T0),
            new VoiceSample(VoiceSampleSource.Uploaded, path, new string('a', 64), hash, 21.4, 0.78)), T0);
        await AddAsync(profile);
        return profile;
    }

    /// <summary>Writes a deterministic file of the given size and returns its SHA-256.</summary>
    public async Task<string> FileAsync(string relativePath, int size)
    {
        var bytes = Encoding.UTF8.GetBytes(relativePath.PadRight(size, '.'))[..size];
        await WriteAsync(relativePath, bytes);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    public string PathOf(string relativePath) => Path.Combine(DataDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Every file below the data directory except the database and library-care, relative with '/'.</summary>
    public IReadOnlyDictionary<string, string> LibraryFiles() => Directory
        .EnumerateFiles(DataDir, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(DataDir, path).Replace('\\', '/'))
        .Where(path => !path.StartsWith("stemstudio.db", StringComparison.Ordinal) && !path.StartsWith("library-care/", StringComparison.Ordinal))
        .ToDictionary(path => path, path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(PathOf(path)))), StringComparer.Ordinal);

    public static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    public Task AddAsync(object entity) => factory.WithScopeAsync(async sp =>
    {
        var context = sp.GetRequiredService<StemStudioDbContext>();
        context.Add(entity);
        await context.SaveChangesAsync(Ct);
        return true;
    });

    private async Task WriteAsync(string relativePath, byte[] bytes)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, Ct);
    }
}
