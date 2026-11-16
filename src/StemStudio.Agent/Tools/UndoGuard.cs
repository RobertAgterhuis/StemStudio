using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Model;

namespace StemStudio.Agent.Tools;

/// <summary>
/// The precondition of an undo call on a mix: the mix must still have the <c>updatedAt</c> the agent's step left, otherwise
/// it was changed outside the agent and the entry is obsolete. Providers hand it to the recorder as the reserved argument
/// <c>$expect</c>; the recorder stores it as <see cref="UndoEntry.ExpectedUpdatedAt"/> (data-model 4.4) and strips the
/// argument. Entries stored before that still carry <c>$expect</c> and are read through <see cref="Split"/>.
/// </summary>
/// <param name="MixId">Mix identifier.</param>
/// <param name="UpdatedAt">Expected last change.</param>
public sealed record UndoGuard(Guid MixId, DateTimeOffset UpdatedAt)
{
    /// <summary>The reserved argument name.</summary>
    public const string Property = "$expect";

    /// <summary>Changes closer than this count as unchanged (storage may round timestamps).</summary>
    public static TimeSpan Tolerance { get; } = TimeSpan.FromSeconds(1);

    /// <summary>Splits stored undo arguments into the tool's arguments and the guard.</summary>
    /// <param name="stored">Stored canonical JSON.</param>
    /// <returns>Canonical tool arguments and the guard, when any.</returns>
    public static (string Arguments, UndoGuard? Guard) Split(string stored)
    {
        var node = JsonNode.Parse(stored)?.AsObject() ?? [];
        var guard = node[Property] is JsonObject expect
            ? new UndoGuard(expect["mixId"]!.GetValue<Guid>(), expect["updatedAt"]!.GetValue<DateTimeOffset>())
            : null;
        node.Remove(Property);
        return (CanonicalJson.Canonicalize(node.ToJsonString()), guard);
    }

    /// <summary>The tool arguments and the guard of a stored entry: <see cref="UndoEntry.ExpectedUpdatedAt"/> with the
    /// arguments' <c>mixId</c>, or the legacy <c>$expect</c> argument.</summary>
    /// <param name="entry">Stored entry.</param>
    /// <returns>Canonical tool arguments and the guard, when any.</returns>
    public static (string Arguments, UndoGuard? Guard) For(UndoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var (arguments, legacy) = Split(entry.Action.Arguments);
        return entry.ExpectedUpdatedAt is { } expected ? (arguments, new UndoGuard(MixIdOf(arguments), expected)) : (arguments, legacy);
    }

    private static Guid MixIdOf(string arguments) => JsonNode.Parse(arguments)!["mixId"]!.GetValue<Guid>();

    /// <summary>True when the mix changed since the step.</summary>
    /// <param name="current">The mix's current <c>updatedAt</c>.</param>
    /// <returns>True when changed.</returns>
    public bool ChangedSince(DateTimeOffset current) => (current - UpdatedAt).Duration() > Tolerance;

    /// <summary>Parses JSON for <see cref="JsonElement"/> consumers.</summary>
    /// <param name="json">Canonical JSON.</param>
    /// <returns>The root element.</returns>
    public static JsonElement Element(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
