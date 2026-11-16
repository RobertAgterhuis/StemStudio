using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Generation;

/// <summary>Error codes of a generation (generator contract 1.1.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GenerationErrorCodeDto>))]
public enum GenerationErrorCodeDto
{
    /// <summary>The GPU became unusable.</summary>
    [JsonStringEnumMemberName("gpuRequired")]
    GpuRequired,

    /// <summary>Not enough free GPU memory within the wait limit.</summary>
    [JsonStringEnumMemberName("gpuBusy")]
    GpuBusy,

    /// <summary>Out of GPU memory twice.</summary>
    [JsonStringEnumMemberName("outOfMemory")]
    OutOfMemory,

    /// <summary>The model could not be downloaded.</summary>
    [JsonStringEnumMemberName("modelDownloadFailed")]
    ModelDownloadFailed,

    /// <summary>The generator restarted, or lost the job.</summary>
    [JsonStringEnumMemberName("interrupted")]
    Interrupted,

    /// <summary>The audio could not be processed.</summary>
    [JsonStringEnumMemberName("invalidInput")]
    InvalidInput,

    /// <summary>Any other model failure.</summary>
    [JsonStringEnumMemberName("engineError")]
    EngineError,

    /// <summary>The vocal stem has almost no singing (1.9.0).</summary>
    [JsonStringEnumMemberName("unvoicedTarget")]
    UnvoicedTarget,

    /// <summary>The voice sample could not be used (1.9.0).</summary>
    [JsonStringEnumMemberName("promptInvalid")]
    PromptInvalid,
}
