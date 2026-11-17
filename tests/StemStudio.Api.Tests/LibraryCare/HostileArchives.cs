using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>Builds small archives with a valid manifest and one defect each (research R-06 H1-H17, in C# for the backend tests).</summary>
internal static class HostileArchives
{
    public const string Track = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f";

    public static readonly byte[] Stem = Encoding.UTF8.GetBytes(new string('s', 64));

    /// <summary>An entry to write: name, bytes, the size the manifest declares, and whether the manifest lists it.</summary>
    public sealed record Entry(string Name, byte[] Bytes, long? DeclaredSize = null, bool Listed = true, string? DeclaredHash = null, int? UnixMode = null);

    public static Entry StemEntry(string name = $"files/tracks/{Track}/stems/vocals.flac") => new(name, Stem);

    public static void Write(string path, IReadOnlyList<Entry> entries, Action<Dictionary<string, object?>>? changeManifest = null, byte[]? database = null)
    {
        var all = new List<Entry> { new("db/stemstudio.db", database ?? Encoding.UTF8.GetBytes("db")) };
        all.AddRange(entries);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in all)
        {
            var zipEntry = zip.CreateEntry(entry.Name, CompressionLevel.NoCompression);
            if (entry.UnixMode is { } mode)
            {
                zipEntry.ExternalAttributes = mode << 16;
            }

            using var stream = zipEntry.Open();
            stream.Write(entry.Bytes);
        }

        var manifest = Manifest(all.Where(entry => entry.Listed).ToList());
        changeManifest?.Invoke(manifest);
        using var manifestStream = zip.CreateEntry("manifest.json").Open();
        JsonSerializer.Serialize(manifestStream, manifest);
    }

    private static Dictionary<string, object?> Manifest(IReadOnlyList<Entry> entries)
    {
        var listed = entries.Select(entry => new Dictionary<string, object?>
        {
            ["path"] = entry.Name,
            ["category"] = entry.Name == "db/stemstudio.db" ? "database" : "stems",
            ["sizeBytes"] = entry.DeclaredSize ?? entry.Bytes.Length,
            ["sha256"] = entry.DeclaredHash ?? Convert.ToHexStringLower(SHA256.HashData(entry.Bytes)),
        }).ToList();
        return new Dictionary<string, object?>
        {
            ["format"] = "stemstudio-backup",
            ["formatVersion"] = "1.0.0",
            ["backupId"] = "5b0f6a3e-2c1d-4e8f-9a7b-1c2d3e4f5a6b",
            ["createdAt"] = "2026-11-16T14:22:33Z",
            ["app"] = new { name = "StemStudio", version = "1.12.0", backendApi = "1.12.0" },
            ["schema"] = new { provider = "sqlite", lastMigration = "20261114215330_Initial", migrations = new[] { "20261114215330_Initial" } },
            ["contents"] = new { included = new[] { "database", "stems" }, excluded = Array.Empty<object>() },
            ["counts"] = new { tracks = 1, mixes = 0, assets = 0, generations = 0, styles = 0, lyricsSheets = 0, voiceProfiles = 0, artists = 0 },
            ["totals"] = new { entries = listed.Count, bytes = listed.Sum(entry => Convert.ToInt64(entry["sizeBytes"], System.Globalization.CultureInfo.InvariantCulture)) },
            ["hashAlgorithm"] = "sha-256",
            ["encryption"] = "none",
            ["entries"] = listed,
            ["voiceProfiles"] = Array.Empty<object>(),
            ["warnings"] = Array.Empty<object>(),
        };
    }
}
