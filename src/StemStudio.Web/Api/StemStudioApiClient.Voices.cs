using System.Text.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;
using StemStudio.Web.Services;

namespace StemStudio.Web.Api;

/// <summary>Voice profile, consent and conversion operations (feature 010, backend contract 1.9.0).</summary>
public sealed partial class StemStudioApiClient
{
    /// <inheritdoc />
    public Task<IReadOnlyList<VoiceProfileDto>> ListVoiceProfilesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<VoiceProfileDto>>(HttpMethod.Get, ApiRoutes.VoiceProfiles, null, cancellationToken);

    /// <inheritdoc />
    public Task<ConsentStatementDto> GetVoiceConsentAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ConsentStatementDto>(HttpMethod.Get, ApiRoutes.VoiceConsent, null, cancellationToken);

    /// <inheritdoc />
    public Task<VoiceProfileDto> CreateVoiceProfileAsync(VoiceProfileUpload upload, CancellationToken cancellationToken = default)
    {
        var body = MultipartBody(upload.Sample, VoiceProfileForm.Sample);
        AddField(body, VoiceProfileForm.DisplayName, upload.DisplayName);
        AddField(body, VoiceProfileForm.PersonName, upload.PersonName);
        AddField(body, VoiceProfileForm.ConsentConfirmed, JsonSerializer.Serialize(upload.ConsentConfirmed));
        AddField(body, VoiceProfileForm.ConsentStatementVersion, upload.ConsentStatementVersion);
        AddField(body, VoiceProfileForm.Source, WireName.Of(upload.Source));
        return SendAsync<VoiceProfileDto>(HttpMethod.Post, ApiRoutes.VoiceProfiles, body, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteVoiceProfileAsync(Guid profileId, bool deleteTakes, CancellationToken cancellationToken = default)
    {
        var uri = $"{ApiRoutes.VoiceProfile(profileId)}?deleteTakes={JsonSerializer.Serialize(deleteTakes)}";
        using var response = await SendRawAsync(HttpMethod.Delete, uri, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<GenerationDto> CreateVoiceConversionAsync(Guid generationId, int index, VoiceConversionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<GenerationDto>(HttpMethod.Post, ApiRoutes.VoiceConversions(generationId, index), JsonBody(request), cancellationToken);
}
