using StemStudio.Contracts;
using StemStudio.Contracts.Library;

namespace StemStudio.Web.Library;

/// <summary>Whose library info the dialog edits (feature 022, FR-031).</summary>
public enum LibraryInfoScope
{
    /// <summary>A track: every item of it without own names moves.</summary>
    Track,

    /// <summary>One library item: only it moves.</summary>
    Item,
}

/// <summary>What the library info dialog edits.</summary>
/// <param name="Scope">Track or item.</param>
/// <param name="Id">The track or item id.</param>
/// <param name="Subject">What is named in the dialog, e.g. the song or item name.</param>
/// <param name="FollowAssetId">Track scope: an item of the track without own names, whose new place the view follows.</param>
public sealed record LibraryInfoTarget(LibraryInfoScope Scope, Guid Id, string Subject, Guid? FollowAssetId = null);

/// <summary>One name of the dialog: the typed text or "Automatic", and the automatic name with its source.</summary>
/// <param name="level">The wire name of the level: <c>artist</c>, <c>album</c> or <c>song</c>.</param>
/// <param name="label">The visible label.</param>
public sealed class LibraryInfoField(string level, string label)
{
    /// <summary>The wire name of the level (also the id suffix of the inputs).</summary>
    public string Level { get; } = level;

    /// <summary>The visible label.</summary>
    public string Label { get; } = label;

    /// <summary>The typed name.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>True when automatic filing chooses the name.</summary>
    public bool Automatic { get; set; } = true;

    /// <summary>The name automatic filing would use.</summary>
    public string AutomaticName { get; private set; } = string.Empty;

    /// <summary>Where the automatic name comes from, e.g. "from band info".</summary>
    public string AutomaticSource { get; private set; } = string.Empty;

    /// <summary>Why the name cannot be saved, or null.</summary>
    public string? Problem { get; set; }

    /// <summary>Takes over the state of the API.</summary>
    /// <param name="state">The level state.</param>
    public void Load(LibraryInfoLevelDto state)
    {
        (Text, Automatic, AutomaticName, AutomaticSource, Problem) = (state.Value ?? string.Empty, state.Value is null, state.Automatic, LibraryInfoForm.SourceText(state.Source), null);
    }

    /// <summary>The value to send: null for automatic, else the trimmed name.</summary>
    /// <returns>The value.</returns>
    public string? Value() => Automatic ? null : Text.Trim();
}

/// <summary>
/// The three names of the library info dialog (feature 022, FR-031, FR-033): validation as the API (1-200 characters
/// after trimming, no control characters) and the request.
/// </summary>
public sealed class LibraryInfoForm
{
    /// <summary>The message of an invalid name.</summary>
    public const string InvalidName = "Use 1 to 200 characters without control characters, or tick Automatic.";

    /// <summary>Artist, album and song, in that order.</summary>
    public IReadOnlyList<LibraryInfoField> Fields { get; } = [new("artist", "Artist"), new("album", "Album"), new("song", "Song")];

    /// <summary>Where a name comes from, as shown next to "Automatic".</summary>
    /// <param name="source">The source.</param>
    /// <returns>For example "from band info".</returns>
    public static string SourceText(PlacementSourceDto source) => source switch
    {
        PlacementSourceDto.Match => "from band info",
        PlacementSourceDto.Import => "from the import",
        PlacementSourceDto.Track => "from the track's library info",
        PlacementSourceDto.Item => "set for this item",
        _ => "fallback",
    };

    /// <summary>Takes over the state of the API.</summary>
    /// <param name="state">The state.</param>
    public void Load(LibraryInfoStateDto state)
    {
        Fields[0].Load(state.Artist);
        Fields[1].Load(state.Album);
        Fields[2].Load(state.Song);
    }

    /// <summary>Checks every name and sets its problem.</summary>
    /// <returns>True when the form can be saved.</returns>
    public bool Validate()
    {
        foreach (var field in Fields)
        {
            field.Problem = field.Value() is { } value && !IsValid(value) ? InvalidName : null;
        }

        return Fields.All(f => f.Problem is null);
    }

    /// <summary>Sets the field messages of a 400 answer (keys <c>artist</c>, <c>album</c>, <c>song</c>).</summary>
    /// <param name="errors">The field errors.</param>
    /// <returns>True when at least one field took a message.</returns>
    public bool ShowErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        var shown = false;
        foreach (var field in Fields)
        {
            if (errors.TryGetValue(field.Level, out var messages) && messages.Length > 0)
            {
                (field.Problem, shown) = (string.Join(" ", messages), true);
            }
        }

        return shown;
    }

    /// <summary>The request of the form.</summary>
    /// <returns>The names, null where automatic.</returns>
    public LibraryInfoRequest Request() => new() { Artist = Fields[0].Value(), Album = Fields[1].Value(), Song = Fields[2].Value() };

    private static bool IsValid(string value) =>
        value.Length is > 0 and <= ContractLimits.LibraryNameMaxLength && !value.Any(char.IsControl);
}
