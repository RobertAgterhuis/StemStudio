using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// JSON text form of a <see cref="TrackArtwork"/> (columns <c>Tracks.Artwork</c> and <c>Tracks.AutoArtwork</c>, null = no
/// picture; feature 005). The source is stored by name; the derived version is not stored. The record compares by value.
/// </summary>
public static class TrackArtworkJson
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Converter between the record and its JSON text (EF Core never passes null to a converter).</summary>
    public static ValueConverter<TrackArtwork?, string> Converter { get; } = new(artwork => Serialize(artwork!), json => Deserialize(json));

    /// <summary>Serializes a record.</summary>
    /// <param name="artwork">The record.</param>
    /// <returns>JSON text.</returns>
    public static string Serialize(TrackArtwork artwork)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        return JsonSerializer.Serialize(new Row(artwork.Source, artwork.Sha256, artwork.UpdatedAt), Options);
    }

    /// <summary>Reads a record; the domain rules are checked again by its constructor.</summary>
    /// <param name="json">JSON text written by <see cref="Serialize"/>.</param>
    /// <returns>The record.</returns>
    /// <exception cref="JsonException">The text is not an artwork record.</exception>
    public static TrackArtwork Deserialize(string json)
    {
        var row = JsonSerializer.Deserialize<Row>(json, Options) ?? throw new JsonException("The stored artwork is null.");
        return new TrackArtwork(row.Source, row.Sha256, DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc));
    }

    private sealed record Row(ArtworkSource Source, string Sha256, DateTime UpdatedAt);
}
