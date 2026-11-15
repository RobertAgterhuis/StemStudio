using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Generation;

/// <summary>Status of a generation (1.8.0; waitingForStems 1.9.0). Completed, failed and cancelled are terminal.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GenerationStateDto>))]
public enum GenerationStateDto
{
    /// <summary>Waiting in the generator queue.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>Voice conversion only: the source take is being imported and separated (API side, 1.9.0).</summary>
    [JsonStringEnumMemberName("waitingForStems")]
    WaitingForStems,

    /// <summary>Waiting for the GPU lease or enough free GPU memory.</summary>
    [JsonStringEnumMemberName("waitingForGpu")]
    WaitingForGpu,

    /// <summary>Downloading the model on first use.</summary>
    [JsonStringEnumMemberName("downloadingModel")]
    DownloadingModel,

    /// <summary>Generating.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Takes are available.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>See the error.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Cancelled by the user.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
