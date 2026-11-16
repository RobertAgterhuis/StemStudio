using StemStudio.Contracts.Generation;

namespace StemStudio.Agent.Ports;

/// <summary>
/// Own-voice endpoints of the public API (feature 010) over loopback. Deliberately read-and-convert only: creating or
/// deleting a voice profile and giving consent stay in the 010 Voices page (FR-025, ADR-019); profiles come back as
/// <see cref="VoiceProfileSummary"/> without the person's name, the sample or its URL.
/// </summary>
public interface IVoiceActions
{
    /// <summary>GET /api/voice-profiles (list_voice_profiles), reduced to summaries.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The profiles.</returns>
    Task<IReadOnlyList<VoiceProfileSummary>> ListVoiceProfilesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// POST /api/generations/{generationId}/takes/{index}/voice-conversions (convert_take_voice, heavy). The backend's
    /// consent check is authoritative (409 consent-required / consent-outdated surface as <see cref="AgentConflictException"/>).
    /// </summary>
    /// <param name="generationId">Song generation.</param>
    /// <param name="index">Take number, from 1.</param>
    /// <param name="request">Profile and pitch options.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued conversion (kind voiceConversion).</returns>
    Task<GenerationDto> CreateVoiceConversionAsync(Guid generationId, int index, VoiceConversionRequest request, CancellationToken cancellationToken);
}
