using System.Net;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Voice profiles, consent and conversions of the typed client (backend contract 1.9.0, feature 010).</summary>
public sealed partial class StemStudioApiClientTests
{
    [Fact]
    public async Task VoiceReads_UseTheirRoutes()
    {
        _handler.Respond(HttpStatusCode.OK, Body(new[] { VoiceData.Profile() })).Respond(HttpStatusCode.OK, Body(VoiceData.Statement()));

        var profiles = await _client.ListVoiceProfilesAsync(Ct);
        var statement = await _client.GetVoiceConsentAsync(Ct);

        profiles.ShouldHaveSingleItem().DisplayName.ShouldBe("My voice");
        (statement.Version, statement.TextNl).ShouldBe(("consent.v1", VoiceData.StatementNl));
        _handler.Requests.Select(r => r.PathAndQuery).ShouldBe(["/api/voice-profiles", "/api/consent/voice"]);
    }

    [Fact]
    public async Task CreateVoiceProfile_SendsTheSampleAndTheConsent()
    {
        _handler.Respond(HttpStatusCode.Created, Body(VoiceData.Profile()));
        await using var stream = new MemoryStream(new byte[2048]);
        var upload = new VoiceProfileUpload(new UploadFile(stream, "recording.webm", "audio/webm"), "My voice", "Sam Example", true, "consent.v1", VoiceSampleSourceDto.Recorded);

        var profile = await _client.CreateVoiceProfileAsync(upload, Ct);

        profile.Id.ShouldBe(VoiceData.ProfileId);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, "/api/voice-profiles"));
        sent.Parts.Select(p => (p.Name, p.FileName)).ShouldBe(
        [
            ("sample", "recording.webm"), ("displayName", null), ("personName", null), ("consentConfirmed", null),
            ("consentStatementVersion", null), ("source", null),
        ]);
        sent.Parts.Single(p => p.Name == "sample").Length.ShouldBe(2048);
    }

    [Theory]
    [InlineData(false, "/api/voice-profiles/6b1a3f2e-0d9c-4b8a-9f7e-d6c5b4a3f2e1?deleteTakes=false")]
    [InlineData(true, "/api/voice-profiles/6b1a3f2e-0d9c-4b8a-9f7e-d6c5b4a3f2e1?deleteTakes=true")]
    public async Task DeleteVoiceProfile_SaysWhetherTheTakesGoToo(bool deleteTakes, string expected)
    {
        _handler.Respond(HttpStatusCode.NoContent);

        await _client.DeleteVoiceProfileAsync(VoiceData.ProfileId, deleteTakes, Ct);

        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Delete, expected));
    }

    [Fact]
    public async Task CreateVoiceConversion_PostsToTheTake()
    {
        _handler.Respond(HttpStatusCode.Accepted, Body(VoiceData.Conversion()));

        var conversion = await _client.CreateVoiceConversionAsync(SongData.SongId, 1, new VoiceConversionRequest { ProfileId = VoiceData.ProfileId, PitchShift = -2, AutoShift = false }, Ct);

        conversion.Kind.ShouldBe(GenerationKindDto.VoiceConversion);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, $"/api/generations/{SongData.SongId}/takes/1/voice-conversions"));
        sent.Body!.ShouldContain("\"pitchShift\":-2");
        sent.Body!.ShouldContain("\"autoShift\":false");
    }
}
