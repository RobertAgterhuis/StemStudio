using StemStudio.Contracts.Recording;
using StemStudio.Domain;
using StemStudio.Domain.Recording;

namespace StemStudio.Api.Recording;

/// <summary>Field rules of the upload metadata (contract bounds of <c>RecordingTakeUpload</c> and <c>ReplaceTakeAudioMetadata</c>).</summary>
public static class TakeUploadRules
{
    /// <summary>The violations of a new take's metadata; empty when valid.</summary>
    /// <param name="metadata">The metadata.</param>
    /// <param name="header">The WAV format.</param>
    /// <returns>Errors with metadata field names.</returns>
    public static IReadOnlyList<DomainError> Validate(RecordingTakeUploadDto metadata, TakeWavHeader header)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(header);
        var errors = new List<DomainError>(TakeRules.SpanErrors(metadata.StartSample, metadata.LengthSamples));
        Check(errors, metadata.PassIndex is null or >= 1 and <= RecordingLimits.MaxPassesPerSession, "passIndex", $"Must be between 1 and {RecordingLimits.MaxPassesPerSession}.");
        Check(errors, metadata.LatencySamples is >= 0 and <= RecordingLimits.MaxLatencySamples, "latencySamples", $"Must be between 0 and {RecordingLimits.MaxLatencySamples}.");
        Check(errors, metadata.Dropouts >= 0, "dropouts", "Must be 0 or more.");
        Check(errors, metadata.DeviceLabel is null || metadata.DeviceLabel.Length <= RecordingLimits.MaxDeviceLabelLength, "deviceLabel", $"Must be at most {RecordingLimits.MaxDeviceLabelLength} characters long.");
        Check(errors, Enum.IsDefined(metadata.Part), "part", "Not a recording part.");
        Check(errors, Enum.IsDefined(metadata.LatencyMethod), "latencyMethod", "Not a latency method.");
        Check(errors, ChannelsMatch(metadata.InputMode, header.Channels), "inputMode", "Mono modes need a 1-channel WAV and stereo a 2-channel WAV.");
        errors.AddRange(NameErrors(metadata.Name));
        return errors;
    }

    /// <summary>The violations of a replacement's metadata; empty when valid.</summary>
    /// <param name="metadata">The metadata.</param>
    /// <returns>Errors with metadata field names.</returns>
    public static IReadOnlyList<DomainError> Validate(ReplaceTakeAudioMetadataDto metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var errors = new List<DomainError>(TakeRules.SpanErrors(metadata.StartSample, metadata.LengthSamples));
        Check(errors, TakeRules.IsValidAdjustment(metadata.AdjustmentSamples), "adjustmentSamples", $"Must be between -{RecordingLimits.MaxAdjustmentSamples} and {RecordingLimits.MaxAdjustmentSamples}.");
        return errors;
    }

    /// <summary>The domain part.</summary>
    /// <param name="part">The DTO part.</param>
    /// <returns>The part.</returns>
    public static RecordingPart ToDomain(RecordingPartDto part) => Enum.Parse<RecordingPart>(part.ToString());

    /// <summary>The domain input mode.</summary>
    /// <param name="mode">The DTO mode.</param>
    /// <returns>The mode.</returns>
    public static InputChannelMode ToDomain(InputChannelModeDto mode) => Enum.Parse<InputChannelMode>(mode.ToString());

    /// <summary>The domain latency method.</summary>
    /// <param name="method">The DTO method.</param>
    /// <returns>The method.</returns>
    public static LatencyMethod ToDomain(LatencyMethodDto method) => Enum.Parse<LatencyMethod>(method.ToString());

    private static bool ChannelsMatch(InputChannelModeDto mode, int channels) => mode switch
    {
        InputChannelModeDto.Stereo => channels == 2,
        InputChannelModeDto.Mono1 or InputChannelModeDto.Mono2 => channels == 1,
        _ => false,
    };

    private static IEnumerable<DomainError> NameErrors(string? name)
    {
        try
        {
            _ = name is null ? null : TakeNaming.Validate(name);
            return [];
        }
        catch (DomainValidationException invalid)
        {
            return invalid.Errors;
        }
    }

    private static void Check(List<DomainError> errors, bool valid, string field, string message)
    {
        if (!valid)
        {
            errors.Add(new DomainError(field, message));
        }
    }
}
