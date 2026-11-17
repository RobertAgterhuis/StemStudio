using System.Text.RegularExpressions;

namespace StemStudio.Domain.LibraryCare;

/// <summary>Kind of a library file in the layout allow-list (data-model 3.3).</summary>
public enum LayoutKind
{
    /// <summary><c>tracks/GUID/stems/STEM.flac</c>.</summary>
    Stem,

    /// <summary><c>tracks/GUID/artwork/auto/(cover|thumb).jpg</c>.</summary>
    AutoArtwork,

    /// <summary><c>tracks/GUID/artwork/user/(cover|thumb).jpg</c>.</summary>
    UserArtwork,

    /// <summary><c>tracks/GUID/detail/PARENT/DETAIL.flac</c>.</summary>
    DetailStem,

    /// <summary><c>uploads/SHA256.(mp3|wav|flac)</c>.</summary>
    Upload,

    /// <summary><c>assets/GUID/NAME</c>.</summary>
    AssetFile,

    /// <summary><c>generations/GUID/NAME(/NAME){0,2}</c>.</summary>
    GenerationFile,

    /// <summary><c>voices/GUID/NAME</c>.</summary>
    VoiceFile,
}

/// <summary>Kind of an archive entry name (backup-archive 1.0.0, data-model 3.3 rules 1 and 2).</summary>
public enum ArchiveEntryKind
{
    /// <summary><c>db/stemstudio.db</c>.</summary>
    Database,

    /// <summary><c>manifest.json</c>.</summary>
    Manifest,

    /// <summary><c>README.txt</c>.</summary>
    Readme,

    /// <summary><c>files/</c> + an allowed library path.</summary>
    LibraryFile,

    /// <summary>A <c>files/</c> directory record (ignored).</summary>
    Directory,

    /// <summary>A safe name outside the layout.</summary>
    Foreign,

    /// <summary>Traversal, absolute, backslash, control characters, too long or empty segments.</summary>
    Unsafe,
}

/// <summary>A library path matched by the layout.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="EntityId">The track, asset, generation or voice profile id; null for uploads.</param>
/// <param name="Parent">The parent stem of a detail stem; null otherwise.</param>
/// <param name="FileName">The last path segment.</param>
public sealed record LayoutMatch(LayoutKind Kind, Guid? EntityId, string? Parent, string FileName);

/// <summary>
/// The binding layout allow-list of data-model 3.3 (archive entries, stored paths, the storage classifier and the clean-up):
/// paths are relative to the data directory with <c>/</c> separators and match a pattern as a whole.
/// </summary>
public static partial class LibraryLayout
{
    /// <summary>Archive entry of the database snapshot.</summary>
    public const string DatabaseEntry = "db/stemstudio.db";

    /// <summary>Archive entry of the manifest (always the last entry).</summary>
    public const string ManifestEntry = "manifest.json";

    /// <summary>Archive entry of the human-readable explanation.</summary>
    public const string ReadmeEntry = "README.txt";

    /// <summary>Prefix of every library file entry.</summary>
    public const string FilesPrefix = "files/";

    /// <summary>Longest entry name accepted.</summary>
    public const int MaxEntryNameLength = 260;

    private const string Guid = "(?<id>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})";
    private const string Name = @"(?!\.{1,2}(/|$))(?![^/]*\.\.)[A-Za-z0-9][A-Za-z0-9._-]{0,99}";

    private static readonly (LayoutKind Kind, Regex Pattern)[] Patterns =
    [
        (LayoutKind.Stem, Rx($"tracks/{Guid}/stems/(?<file>(vocals|drums|bass|guitar|piano|other)\\.flac)")),
        (LayoutKind.AutoArtwork, Rx($"tracks/{Guid}/artwork/auto/(?<file>(cover|thumb)\\.jpg)")),
        (LayoutKind.UserArtwork, Rx($"tracks/{Guid}/artwork/user/(?<file>(cover|thumb)\\.jpg)")),
        (LayoutKind.DetailStem, Rx($"tracks/{Guid}/detail/(?<parent>vocals|drums|guitar|piano|other)/(?<file>(kick|snare|toms|hihat|cymbals|lead|backing|strings|brass|woodwinds|keys|synth|acoustic-guitar|electric-guitar|percussion|rest)\\.flac)")),
        (LayoutKind.Upload, Rx("uploads/(?<file>[0-9a-f]{64}\\.(mp3|wav|flac))")),
        (LayoutKind.AssetFile, Rx($"assets/{Guid}/(?<file>{Name})")),
        (LayoutKind.GenerationFile, Rx($"generations/{Guid}/({Name}/){{0,2}}(?<file>{Name})")),
        (LayoutKind.VoiceFile, Rx($"voices/{Guid}/(?<file>{Name})")),
    ];

    /// <summary>True when the data-relative path matches one layout pattern.</summary>
    /// <param name="relativePath">Path relative to the data directory.</param>
    /// <returns>True when allowed.</returns>
    public static bool IsAllowed(string relativePath) => Match(relativePath) is not null;

    /// <summary>Matches a data-relative path against the layout.</summary>
    /// <param name="relativePath">Path relative to the data directory.</param>
    /// <returns>The match, or null when the path is outside the layout.</returns>
    public static LayoutMatch? Match(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        foreach (var (kind, pattern) in Patterns)
        {
            var match = pattern.Match(relativePath);
            if (match.Success)
            {
                return ToMatch(kind, match);
            }
        }

        return null;
    }

    /// <summary>
    /// True when an archive entry name is safe to map to a path: 1-260 characters, <c>/</c> separators only, no control
    /// characters, not absolute, no drive, no empty, <c>.</c> or <c>..</c> segment (a trailing <c>/</c> marks a directory).
    /// </summary>
    /// <param name="name">The entry name as stored in the central directory.</param>
    /// <returns>True when safe.</returns>
    public static bool IsSafeEntryName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length is 0 or > MaxEntryNameLength || name.Any(IsForbiddenCharacter))
        {
            return false;
        }

        var segments = (name.EndsWith('/') ? name[..^1] : name).Split('/');
        return segments.All(segment => segment.Length > 0 && segment is not ("." or ".."));
    }

    /// <summary>Classifies an archive entry name.</summary>
    /// <param name="name">The entry name.</param>
    /// <returns>The kind; <see cref="ArchiveEntryKind.Unsafe"/> before anything else.</returns>
    public static ArchiveEntryKind ClassifyEntry(string name)
    {
        if (!IsSafeEntryName(name))
        {
            return ArchiveEntryKind.Unsafe;
        }

        return name switch
        {
            DatabaseEntry => ArchiveEntryKind.Database,
            ManifestEntry => ArchiveEntryKind.Manifest,
            ReadmeEntry => ArchiveEntryKind.Readme,
            _ => ClassifyFileEntry(name),
        };
    }

    /// <summary>The archive entry name of a library file.</summary>
    /// <param name="relativePath">Path relative to the data directory.</param>
    /// <returns><c>files/</c> + the path.</returns>
    public static string ToEntryName(string relativePath) => FilesPrefix + relativePath;

    /// <summary>The data-relative path of a library file entry.</summary>
    /// <param name="entryName">An entry name starting with <c>files/</c>.</param>
    /// <returns>The path without the prefix.</returns>
    /// <exception cref="ArgumentException">The entry is not a library file entry.</exception>
    public static string ToDataPath(string entryName)
    {
        ArgumentNullException.ThrowIfNull(entryName);
        return entryName.StartsWith(FilesPrefix, StringComparison.Ordinal)
            ? entryName[FilesPrefix.Length..]
            : throw new ArgumentException($"'{entryName}' is not a library file entry.", nameof(entryName));
    }

    private static ArchiveEntryKind ClassifyFileEntry(string name)
    {
        if (!name.StartsWith(FilesPrefix, StringComparison.Ordinal))
        {
            return ArchiveEntryKind.Foreign;
        }

        if (name.EndsWith('/'))
        {
            return ArchiveEntryKind.Directory;
        }

        return IsAllowed(name[FilesPrefix.Length..]) ? ArchiveEntryKind.LibraryFile : ArchiveEntryKind.Foreign;
    }

    private static bool IsForbiddenCharacter(char character) => character is '\\' or ':' || char.IsControl(character);

    private static LayoutMatch ToMatch(LayoutKind kind, Match match)
    {
        var id = match.Groups["id"];
        var parent = match.Groups["parent"];
        return new LayoutMatch(
            kind,
            id.Success ? System.Guid.Parse(id.Value) : null,
            parent.Success ? parent.Value : null,
            match.Groups["file"].Value);
    }

    private static Regex Rx(string pattern) =>
        new($"\\A{pattern}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
}
