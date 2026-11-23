using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Persistence.Configurations;

/// <summary>
/// JSON text form of an <see cref="AnalysisDocument"/> (column <c>TrackAnalyses.DocumentJson</c>, feature 016): a
/// <c>$type</c> discriminator (<c>chords</c>, <c>drumHits</c>, <c>vocalPitch</c>), enums by name, computed properties left
/// out. Documents are immutable and replaced on every change, so they compare by reference (no re-serialisation of
/// 90,000 pitch frames on every save). A stored text that cannot be read (an archive from outside) loads as no document.
/// </summary>
public static class AnalysisDocumentJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        IgnoreReadOnlyProperties = true,
        MaxDepth = 16,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Polymorphic } },
    };

    /// <summary>Converter between the document and its JSON text (EF Core never passes null to a converter).</summary>
    public static ValueConverter<AnalysisDocument?, string> Converter { get; } = new(document => Serialize(document!), json => Deserialize(json));

    /// <summary>Compares documents by reference.</summary>
    public static ValueComparer<AnalysisDocument?> Comparer { get; } = new(
        (left, right) => ReferenceEquals(left, right),
        document => document == null ? 0 : RuntimeHelpers.GetHashCode(document),
        document => document);

    /// <summary>Serializes a document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>JSON text.</returns>
    public static string Serialize(AnalysisDocument document) => JsonSerializer.Serialize(document, Options);

    /// <summary>Reads a document; unreadable text gives null.</summary>
    /// <param name="json">JSON text.</param>
    /// <returns>The document, or null.</returns>
    public static AnalysisDocument? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AnalysisDocument>(json, Options);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static void Polymorphic(JsonTypeInfo info)
    {
        if (info.Type != typeof(AnalysisDocument))
        {
            return;
        }

        info.PolymorphismOptions = DocumentTypes();
    }

    /// <summary>The <c>$type</c> discriminators of the three documents; an unknown type fails.</summary>
    private static JsonPolymorphismOptions DocumentTypes() => new()
    {
        TypeDiscriminatorPropertyName = "$type",
        UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        DerivedTypes =
        {
            new JsonDerivedType(typeof(ChordChart), "chords"),
            new JsonDerivedType(typeof(DrumHitsDocument), "drumHits"),
            new JsonDerivedType(typeof(VocalPitchDocument), "vocalPitch"),
        },
    };
}
