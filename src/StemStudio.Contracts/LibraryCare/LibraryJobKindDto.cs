using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Kind of a library job (<c>LibraryJobKind</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LibraryJobKindDto>))]
public enum LibraryJobKindDto
{
    /// <summary>Writes a backup archive.</summary>
    [JsonStringEnumMemberName("backup")]
    Backup,

    /// <summary>Reads an archive and checks every entry.</summary>
    [JsonStringEnumMemberName("verify")]
    Verify,

    /// <summary>Restores an archive (replace or merge).</summary>
    [JsonStringEnumMemberName("restore")]
    Restore,

    /// <summary>Switches back to the previous library.</summary>
    [JsonStringEnumMemberName("undoRestore")]
    UndoRestore,
}
