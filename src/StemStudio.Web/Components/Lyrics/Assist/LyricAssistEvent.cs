using System.Text.Json;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>One event of an assist's stream (data-model section 10): its sequence number, type and JSON data.</summary>
/// <param name="Id">The sequence number (<c>id:</c>), used to resume with <c>after</c>.</param>
/// <param name="Type">The event type, one of <see cref="LyricAssistEventTypes"/>.</param>
/// <param name="Data">The JSON data.</param>
public sealed record LyricAssistEvent(int Id, string Type, JsonElement Data)
{
    /// <summary>True for completed, failed and cancelled.</summary>
    public bool IsTerminal => LyricAssistEventTypes.IsTerminal(Type);

    /// <summary>Reads the data as a contract DTO.</summary>
    /// <typeparam name="T">The DTO type of this event type.</typeparam>
    /// <returns>The DTO.</returns>
    /// <exception cref="JsonException">The data is null or does not match.</exception>
    public T Read<T>() => Data.Deserialize<T>(StemStudioApiClient.Json) ?? throw new JsonException($"The {Type} event has no data.");
}
