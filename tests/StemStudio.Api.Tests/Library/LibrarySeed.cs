using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.Library;

/// <summary>
/// The 022 performance library (T044): 5,000 items of 500 tracks by 150 artists (every instrument family and detail
/// stem, mixes and sheet music), written unfiled as a pre-022 library has them. Also writes the E2E database fixtures.
/// </summary>
internal static class LibrarySeed
{
    /// <summary>Environment variable naming the folder the E2E fixtures are written to (qa).</summary>
    public const string OutputVariable = "STEMSTUDIO_LIBRARY_SEED_OUT";

    /// <summary>Fixture of a library from before 022 (migrated to the last pre-022 migration, nothing filed).</summary>
    public const string Pre022File = "library-5000-pre022.db";

    /// <summary>Fixture of a filed 022 library.</summary>
    public const string CurrentFile = "library-5000.db";

    /// <summary>Last migration before 022.</summary>
    public const string LastMigrationBefore022 = "20261116211814_AddMixPractice";

    public const int Items = 5000;
    public const int Tracks = 500;
    public const int Artists = 150;

    private static readonly string[] Channels =
        ["vocals", "drums", "bass", "guitar", "piano", "other", "drums.snare", "vocals.lead", "guitar.acoustic-guitar", "other.strings"];

    /// <summary>Adds the tracks and the unfiled items to the factory's database.</summary>
    public static Task<bool> SeedAsync(ApiFactory factory) => factory.WithScopeAsync(async sp =>
    {
        var context = sp.GetRequiredService<StemStudioDbContext>();
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        var tracks = Enumerable.Range(0, Tracks).Select(TrackOf).ToList();
        context.Tracks.AddRange(tracks);
        context.Assets.AddRange(Enumerable.Range(0, Items).Select(i => ItemOf(i, tracks[i % Tracks])));
        await context.SaveChangesAsync();
        return true;
    });

    /// <summary>Copies the factory's database to a file (consistent copy through <c>VACUUM INTO</c>).</summary>
    public static async Task CopyAsync(ApiFactory factory, string target)
    {
        File.Delete(target);
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(factory.DataDir, "stemstudio.db")};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $target";
        command.Parameters.AddWithValue("$target", target);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Migrates a database file down to the last pre-022 migration.</summary>
    public static async Task MigrateToPre022Async(string file)
    {
        var options = new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite($"Data Source={file};Pooling=False").Options;
        await using var context = new StemStudioDbContext(options);
        await context.GetService<IMigrator>().MigrateAsync(LastMigrationBefore022);
    }

    private static Track TrackOf(int i)
    {
        var track = AssetTestData.NamedTrack(VideoId(i));
        var artist = $"Artist {i % Artists:000}";
        LibraryTestData.Match(track, LibraryTestData.Found(i % 3 == 0 ? $"The {artist}" : artist, $"Album {i % 4}", $"Song {i:000}", 1970 + (i % 50)));
        return track;
    }

    private static Asset ItemOf(int i, Track track)
    {
        var channel = Channels[i % Channels.Length];
        var provenance = LibraryTestData.ExportProvenance(track.Id, track.Title, track.Artist, channel);
        if (i % 25 == 0)
        {
            provenance = provenance with { Recipe = provenance.Recipe! with { Mode = ExportMode.Mix, Channels = ["drums", "bass", "vocals.lead"] } };
        }

        return Asset.Create(Guid.NewGuid(), $"{track.Title} - {channel} {i}", provenance, LibraryTestData.T0.AddSeconds(i));
    }

    private static string VideoId(int i) => $"seed{i:0000000}";
}
