using System.Text.Json;

namespace StemStudio.Web.Services;

/// <summary>The contract spelling of an enum value (its <c>JsonStringEnumMemberName</c>), e.g. <c>waitingForGpu</c>.</summary>
public static class WireName
{
    /// <summary>Serialises a contract enum value with its own converter and strips the quotes.</summary>
    /// <typeparam name="T">A contract enum with a string converter.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The wire name, used for query strings and <c>data-*</c> attributes.</returns>
    public static string Of<T>(T value)
        where T : struct, Enum => JsonSerializer.Serialize(value).Trim('"');
}
