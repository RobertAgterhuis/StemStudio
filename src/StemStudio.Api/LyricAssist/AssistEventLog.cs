using System.Text;
using System.Text.Json;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Appends the events of one assist (data-model 10): numbered from 1, JSON payload of the contract DTO (at most 16 KB), stored with
/// every pending change of the unit of work so the replay always matches the stored state.
/// </summary>
/// <param name="assists">Assist repository (saves the event and the tracked changes).</param>
/// <param name="clock">Clock.</param>
public sealed class AssistEventLog(ILyricAssistRepository assists, TimeProvider clock)
{
    /// <summary>JSON of the payloads (camelCase; enums by their contract names).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<Guid, int> _seq = [];

    /// <summary>Appends an event and saves.</summary>
    /// <param name="assistId">Assist id.</param>
    /// <param name="type">Event type (<c>LyricAssistEventTypes</c>).</param>
    /// <param name="payload">The contract DTO.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sequence number.</returns>
    public async Task<int> AppendAsync(Guid assistId, string type, object payload, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.Serialize(payload, payload.GetType(), Json);
        if (Encoding.UTF8.GetByteCount(data) > AssistEvent.MaxPayloadBytes)
        {
            data = "{}";
        }

        var seq = (_seq.TryGetValue(assistId, out var last) ? last : await assists.LastSeqAsync(assistId, cancellationToken)) + 1;
        _seq[assistId] = seq;
        await assists.AddEventAsync(AssistEvent.Create(assistId, seq, (type, data), clock.GetUtcNow().UtcDateTime), cancellationToken);
        return seq;
    }
}
