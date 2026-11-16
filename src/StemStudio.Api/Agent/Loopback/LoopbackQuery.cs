using System.Globalization;
using StemStudio.Api.Agent.Stores;

namespace StemStudio.Api.Agent.Loopback;

/// <summary>Builds query strings of the loopback calls (null values are left out, values are escaped).</summary>
internal static class LoopbackQuery
{
    /// <summary>Appends the non-null parameters to a path.</summary>
    /// <param name="path">The path.</param>
    /// <param name="parameters">Name and value pairs.</param>
    /// <returns>The path with its query.</returns>
    public static string Build(string path, params (string Name, string? Value)[] parameters)
    {
        var pairs = parameters.Where(p => p.Value is not null).Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value!)}").ToList();
        return pairs.Count == 0 ? path : $"{path}?{string.Join('&', pairs)}";
    }

    /// <summary>The contract name of an optional enum value.</summary>
    /// <typeparam name="T">Enum type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The name, or null.</returns>
    public static string? Name<T>(T? value)
        where T : struct, Enum => value is { } some ? AgentStoreValues.ContractName(some) : null;

    /// <summary>An optional id.</summary>
    /// <param name="value">The id.</param>
    /// <returns>Its text, or null.</returns>
    public static string? Id(Guid? value) => value?.ToString();

    /// <summary>An optional number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>Its invariant text, or null.</returns>
    public static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);
}
