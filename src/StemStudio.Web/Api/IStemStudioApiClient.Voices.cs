using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;

namespace StemStudio.Web.Api;

/// <summary>The multipart body of <c>POST /api/voice-profiles</c> (backend contract 1.9.0).</summary>
/// <param name="Sample">The recorded or uploaded sample (10-60 s of own singing, at most 20 MiB).</param>
/// <param name="DisplayName">Profile name, 1-60 characters.</param>
/// <param name="PersonName">Typed name of the person whose voice it is, 1-80 characters.</param>
/// <param name="ConsentConfirmed">The consent checkbox; the page only sends a profile when it is ticked.</param>
/// <param name="ConsentStatementVersion">Version of the statement that was shown and agreed to.</param>
/// <param name="Source">Recorded in the app or uploaded.</param>
public sealed record VoiceProfileUpload(
    UploadFile Sample,
    string DisplayName,
    string PersonName,
    bool ConsentConfirmed,
    string ConsentStatementVersion,
    VoiceSampleSourceDto Source);

/// <summary>Voice profiles with consent and voice conversion (feature 010, backend contract 1.9.0).</summary>
public partial interface IStemStudioApiClient
{
    /// <summary><c>GET /api/voice-profiles</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The profiles.</returns>
    Task<IReadOnlyList<VoiceProfileDto>> ListVoiceProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/consent/voice</c>: the current consent statement in English and Dutch.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The statement and its version.</returns>
    Task<ConsentStatementDto> GetVoiceConsentAsync(CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/voice-profiles</c>: a new profile from an own recording with the consent record.</summary>
    /// <param name="upload">Sample, names and consent.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The profile (201); 400 (consent), 409, 413, 422 (sample refused) and 503 throw <see cref="ApiException"/>.</returns>
    Task<VoiceProfileDto> CreateVoiceProfileAsync(VoiceProfileUpload upload, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE /api/voice-profiles/{id}?deleteTakes=</c>: removes the sample and every derived file.</summary>
    /// <param name="profileId">The profile.</param>
    /// <param name="deleteTakes">Also delete the takes converted to this voice.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the profile is deleted (204).</returns>
    Task DeleteVoiceProfileAsync(Guid profileId, bool deleteTakes, CancellationToken cancellationToken = default);

    /// <summary><c>POST /api/generations/{id}/takes/{index}/voice-conversions</c>: "Use my voice" on a song take.</summary>
    /// <param name="generationId">A completed song generation.</param>
    /// <param name="index">The take with an AI voice.</param>
    /// <param name="request">Profile and pitch.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The conversion (202, queued or waiting for stems); 404 and 409 throw <see cref="ApiException"/>.</returns>
    Task<GenerationDto> CreateVoiceConversionAsync(Guid generationId, int index, VoiceConversionRequest request, CancellationToken cancellationToken = default);
}
