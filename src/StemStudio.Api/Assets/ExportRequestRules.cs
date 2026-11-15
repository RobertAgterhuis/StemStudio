using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Domain;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Rendering;

namespace StemStudio.Api.Assets;

/// <summary>Request-level rules of exports that need no track: format, name and the mix source (data-model 4.2).</summary>
public static class ExportRequestRules
{
    /// <summary>The violations of an export request; empty when valid.</summary>
    /// <param name="request">The request.</param>
    /// <returns>Errors with request paths.</returns>
    public static IReadOnlyList<DomainError> Validate(CreateAudioExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<DomainError>();
        errors.AddRange(FormatErrors(request.Format));
        errors.AddRange(NameErrors(request.Name));
        if (request.Mode == ExportModeDto.Mix && (request.Mix is null) == (request.MixId is null))
        {
            errors.Add(new DomainError("mix", "Send either the mixer state (mix) or a saved mix (mixId)."));
        }

        return errors;
    }

    /// <summary>The violations of an output format.</summary>
    /// <param name="format">The format, or null.</param>
    /// <returns>Errors on <c>format</c>.</returns>
    public static IEnumerable<DomainError> FormatErrors(ExportFormatDto? format)
    {
        var message = format switch
        {
            null => "Choose a format.",
            { Format: AudioFileFormatDto.Mp3, BitDepth: not null } => "MP3 has no bit depth; leave bitDepth empty.",
            { Format: AudioFileFormatDto.Mp3, Mp3BitrateKbps: { } rate } when !ContractLimits.Mp3Bitrates.Contains(rate) => "The MP3 bit rate must be 192, 256 or 320 kbps.",
            { Format: not AudioFileFormatDto.Mp3, Mp3BitrateKbps: not null } => "Only MP3 has a bit rate; leave mp3BitrateKbps empty.",
            { Format: not AudioFileFormatDto.Mp3, BitDepth: { } depth } when !ContractLimits.ExportBitDepths.Contains(depth) => "The bit depth must be 16 or 24.",
            _ => null,
        };
        return message is null ? [] : [new DomainError("format", message)];
    }

    /// <summary>The resolved output format: bit depth 16 for one raw channel, else 24; MP3 at 320 kbps unless chosen.</summary>
    /// <param name="format">A valid format.</param>
    /// <param name="singleRaw">True for a raw render of one channel.</param>
    /// <returns>The format.</returns>
    public static ExportFormat Resolve(ExportFormatDto format, bool singleRaw)
    {
        ArgumentNullException.ThrowIfNull(format);
        return format.Format == AudioFileFormatDto.Mp3
            ? new ExportFormat(AudioFormat.Mp3, null, format.Mp3BitrateKbps ?? 320)
            : new ExportFormat(AssetMapper.ToDomain(format.Format), format.BitDepth ?? (singleRaw ? 16 : 24), null);
    }

    private static IEnumerable<DomainError> NameErrors(string? name)
    {
        try
        {
            _ = name is null ? null : AssetNaming.Validate(name);
            return [];
        }
        catch (DomainValidationException invalid)
        {
            return invalid.Errors;
        }
    }
}
