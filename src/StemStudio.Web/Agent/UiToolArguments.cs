using System.Text.Json;

namespace StemStudio.Web.Agent;

/// <summary>A UI tool failure the user can read (unknown stem, missing argument, invalid loop).</summary>
public sealed class UiToolException : Exception
{
    /// <summary>Creates the exception.</summary>
    public UiToolException()
    {
    }

    /// <summary>Creates the exception with its readable message.</summary>
    /// <param name="message">The message.</param>
    public UiToolException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with its readable message and cause.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public UiToolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Typed access to the JSON arguments of a <c>ui.requested</c> event. The backend validated them against the tool
/// schema (FR-008); a value of the wrong JSON type reads as missing.
/// </summary>
/// <param name="json">The arguments object.</param>
public sealed class UiToolArguments(JsonElement json)
{
    /// <summary>A number, or null.</summary>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    public double? Number(string name) => Get(name) is { ValueKind: JsonValueKind.Number } value ? value.GetDouble() : null;

    /// <summary>A whole number, or null.</summary>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    public int? Whole(string name) => Get(name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number) ? number : null;

    /// <summary>A boolean, or null.</summary>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    public bool? Bool(string name) => Get(name) is { ValueKind: JsonValueKind.True or JsonValueKind.False } value ? value.GetBoolean() : null;

    /// <summary>A string, or null.</summary>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    public string? Text(string name) => Get(name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    /// <summary>A GUID string, or null.</summary>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    public Guid? Id(string name) => Guid.TryParse(Text(name), out var id) ? id : null;

    /// <summary>A required string.</summary>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    /// <exception cref="UiToolException">The argument is missing.</exception>
    public string RequiredText(string name) => Text(name) ?? throw Missing(name);

    /// <summary>A required number.</summary>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    /// <exception cref="UiToolException">The argument is missing.</exception>
    public double RequiredNumber(string name) => Number(name) ?? throw Missing(name);

    /// <summary>A required GUID.</summary>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    /// <exception cref="UiToolException">The argument is missing.</exception>
    public Guid RequiredId(string name) => Id(name) ?? throw Missing(name);

    /// <summary>A required boolean.</summary>
    /// <param name="name">The argument.</param>
    /// <returns>The value.</returns>
    /// <exception cref="UiToolException">The argument is missing.</exception>
    public bool RequiredBool(string name) => Bool(name) ?? throw Missing(name);

    private static UiToolException Missing(string name) => new($"The argument '{name}' is required.");

    private JsonElement? Get(string name) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var value) ? value : null;
}
