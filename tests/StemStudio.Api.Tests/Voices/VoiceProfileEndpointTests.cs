using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Generations;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;
using StemStudio.Domain.Voices;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Voices;

/// <summary>010 T044: voice profiles with consent, the exact-copy guard and the singer's checks (FR-005, FR-006, FR-007, SC-004).</summary>
public sealed class VoiceProfileEndpointTests : ApiTestBase
{
    internal static readonly byte[] OwnVoice = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task Create_StoresTheSampleAndTheConsentRecord()
    {
        using var response = await CreateAsync(Client);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var profile = await response.BodyAsync<VoiceProfileDto>();
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.VoiceProfile(profile.Id));
        (profile.DisplayName, profile.Source, profile.DurationSeconds, profile.VoicedRatio, profile.ConvertedTakeCount).ShouldBe(("My voice", VoiceSampleSourceDto.Recorded, 21.4, 0.78, 0));
        profile.Consent.ShouldBe(new VoiceConsentDto { StatementVersion = "consent.v1", PersonName = "Pat Example", ConsentedAt = ApiFactory.Start });
        profile.SampleUrl.ShouldBe(ApiRoutes.VoiceSample(profile.Id));
        Factory.Singer.Samples.Single().ShouldBe(OwnVoice);
        var stored = await Factory.WithScopeAsync(async sp => (await sp.GetRequiredService<IVoiceProfileRepository>().FindAsync(profile.Id, CancellationToken.None))!);
        (stored.Consent.StatementSha256, stored.SamplePcmSha256, stored.SampleFileSha256)
            .ShouldBe((ConsentStatement.Sha256, "4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d5e6f708192a3b", Convert.ToHexStringLower(SHA256.HashData(OwnVoice))));
        (await File.ReadAllBytesAsync(Path.Combine(Factory.DataDir, "voices", profile.Id.ToString(), "sample.flac"), Ct)).ShouldBe(Factory.Singer.NormalisedSample);
        await ContractAssert.MatchesAsync(response, "createVoiceProfile");
    }

    [Theory]
    [InlineData("false", "consent.v1", "Pat", "consent-required", "consentConfirmed")]
    [InlineData("true", "consent.v0", "Pat", "consent-outdated", "consentStatementVersion")]
    [InlineData("true", "consent.v1", "", null, "personName")]
    public async Task Create_ConsentIsCheckedBeforeAnythingIsStored(string confirmed, string version, string person, string? type, string field)
    {
        using var response = await CreateAsync(Client, form => form with { Confirmed = confirmed, Version = version, Person = person });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        if (type is not null)
        {
            await RestyleEndpointTests.AssertProblemAsync(response, HttpStatusCode.BadRequest, type);
        }

        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        Factory.Singer.Samples.ShouldBeEmpty();
        Directory.Exists(Path.Combine(Factory.DataDir, "voices")).ShouldBeFalse();
        await ContractAssert.MatchesAsync(response, "createVoiceProfile");
    }

    [Fact]
    public async Task Create_NamesAreUniqueAndValidated()
    {
        (await CreateAsync(Client)).Dispose();

        using var taken = await CreateAsync(Client, form => form with { Name = "MY VOICE", Sample = [9, 9] });
        using var invalid = await CreateAsync(Client, form => form with { Name = "", Sample = [9, 8] });
        using var source = await CreateAsync(Client, form => form with { Name = "Other", Source = "stolen", Sample = [9, 7] });

        await RestyleEndpointTests.AssertProblemAsync(taken, HttpStatusCode.Conflict, "voice-name-taken");
        (await invalid.FieldErrorsAsync("displayName")).ShouldNotBeEmpty();
        (await source.FieldErrorsAsync("source")).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(taken, "createVoiceProfile");
    }

    [Fact]
    public async Task Create_TheSingerMustBeReady()
    {
        using var noSinger = new ApiFactory { SingerInstalled = false };
        using var noSingerClient = noSinger.CreateClient();
        using var notInstalled = await CreateAsync(noSingerClient);
        Factory.Singer.HealthExample = "NoGpuHealth";
        using var unavailable = await CreateAsync(Client);

        await RestyleEndpointTests.AssertProblemAsync(notInstalled, HttpStatusCode.Conflict, "voice-not-installed");
        await RestyleEndpointTests.AssertProblemAsync(unavailable, HttpStatusCode.Conflict, "voice-unavailable");
        await ContractAssert.MatchesAsync(notInstalled, "createVoiceProfile");
    }

    [Fact]
    public async Task Create_AudioFromTheLibraryIsRefused()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.Services.GetRequiredService<IStemFileStore>().SaveStemAsync(track.Id, "vocals", (s, ct) => s.WriteAsync(OwnVoice, ct).AsTask(), CancellationToken.None);

        using var response = await CreateAsync(Client);

        var problem = await RestyleEndpointTests.AssertProblemAsync(response, (HttpStatusCode)422, "library-audio");
        problem.GetProperty("detail").GetString().ShouldBe("This file is audio from your StemStudio library. Record or upload your own voice instead.");
        Factory.Singer.Samples.ShouldBeEmpty();
        await ContractAssert.MatchesAsync(response, "createVoiceProfile");
    }

    [Theory]
    [InlineData("unvoiced", "unvoiced")]
    [InlineData("tooShort", "too-short")]
    [InlineData("tooLong", "too-long")]
    public async Task Create_TheSingersCheckIsMapped(string reason, string type)
    {
        Factory.Singer.SampleCheckExample = "VoiceSampleUnvoiced";
        Factory.Singer.ChangeCheck = check => check["reason"] = reason;

        using var response = await CreateAsync(Client);

        var problem = await RestyleEndpointTests.AssertProblemAsync(response, (HttpStatusCode)422, type);
        problem.GetProperty("detail").GetString()!.ShouldStartWith("The recording contains too little singing");
        Directory.Exists(Path.Combine(Factory.DataDir, "voices")).ShouldBeFalse();
        await ContractAssert.MatchesAsync(response, "createVoiceProfile");
    }

    [Fact]
    public async Task Create_LargeSamplesAre413()
    {
        using var response = await CreateAsync(Client, form => form with { Sample = new byte[(20 * 1024 * 1024) + 1] });

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        await ContractAssert.MatchesAsync(response, "createVoiceProfile");
    }

    [Fact]
    public async Task Create_SingerDownIs503()
    {
        Factory.Singer.Override = request => request.RequestUri!.AbsolutePath.EndsWith("/check", StringComparison.Ordinal) ? new HttpResponseMessage(HttpStatusCode.BadGateway) : null;

        using var response = await CreateAsync(Client);

        await RestyleEndpointTests.AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "generator-unavailable");
        await ContractAssert.MatchesAsync(response, "createVoiceProfile");
    }

    [Fact]
    public async Task ListGetSampleAndConsentStatement()
    {
        var profile = await (await CreateAsync(Client)).BodyAsync<VoiceProfileDto>();

        using var list = await GetAsync(ApiRoutes.VoiceProfiles);
        using var one = await GetAsync(ApiRoutes.VoiceProfile(profile.Id));
        using var missing = await GetAsync(ApiRoutes.VoiceProfile(Guid.NewGuid()));
        using var sample = await GetAsync(ApiRoutes.VoiceSample(profile.Id));
        using var partial = await SendAsync(ApiRoutes.VoiceSample(profile.Id), request => request.Headers.Range = new RangeHeaderValue(0, 1));
        using var noSample = await GetAsync(ApiRoutes.VoiceSample(Guid.NewGuid()));
        using var statement = await GetAsync(ApiRoutes.VoiceConsent);

        (await list.BodyAsync<List<VoiceProfileDto>>()).Single().Id.ShouldBe(profile.Id);
        (await sample.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Factory.Singer.NormalisedSample);
        (partial.StatusCode, missing.StatusCode, noSample.StatusCode).ShouldBe((HttpStatusCode.PartialContent, HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        (await statement.BodyAsync<ConsentStatementDto>()).ShouldBe(new ConsentStatementDto { Version = "consent.v1", Text = ConsentStatement.Text, TextNl = ConsentStatement.TextNl });
        await ContractAssert.MatchesAsync(list, "listVoiceProfiles");
        await ContractAssert.MatchesAsync(one, "getVoiceProfile");
        await ContractAssert.MatchesAsync(missing, "getVoiceProfile");
        await ContractAssert.MatchesAsync(sample, "getVoiceSample");
        await ContractAssert.MatchesAsync(partial, "getVoiceSample");
        await ContractAssert.MatchesAsync(noSample, "getVoiceSample");
        await ContractAssert.MatchesAsync(statement, "getVoiceConsentStatement");
    }

    [Fact]
    public async Task Status_ReportsTheSinger()
    {
        using var ready = await GetAsync(ApiRoutes.GenerationStatus);
        (await ready.BodyAsync<GenerationStatusDto>()).Voice.ShouldBe(new VoiceServiceStatusDto { State = GenerationAvailabilityDto.Ready });
        await ContractAssert.MatchesAsync(ready, "getGenerationStatus");

        Factory.Singer.HealthExample = "NoGpuHealth";
        Factory.Clock.Advance(TimeSpan.FromSeconds(11));
        using var unavailable = await GetAsync(ApiRoutes.GenerationStatus);
        (await unavailable.BodyAsync<GenerationStatusDto>()).Voice.State.ShouldBe(GenerationAvailabilityDto.Unavailable);
    }

    internal sealed record ProfileForm(byte[] Sample, string Name, string Person, string Confirmed, string Version, string Source);

    internal static Task<HttpResponseMessage> CreateAsync(HttpClient client, Func<ProfileForm, ProfileForm>? change = null)
    {
        var form = (change ?? (f => f))(new ProfileForm(OwnVoice, "My voice", "Pat Example", "true", "consent.v1", "recorded"));
        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(form.Sample), VoiceProfileForm.Sample, "take.webm" },
            { new StringContent(form.Name), VoiceProfileForm.DisplayName },
            { new StringContent(form.Person), VoiceProfileForm.PersonName },
            { new StringContent(form.Confirmed), VoiceProfileForm.ConsentConfirmed },
            { new StringContent(form.Version), VoiceProfileForm.ConsentStatementVersion },
            { new StringContent(form.Source), VoiceProfileForm.Source },
        };
        return client.PostAsync(new Uri(ApiRoutes.VoiceProfiles, UriKind.Relative), content, TestContext.Current.CancellationToken);
    }

    private Task<HttpResponseMessage> GetAsync(string path) => Client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    private Task<HttpResponseMessage> SendAsync(string path, Action<HttpRequestMessage> configure)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        configure(request);
        return Client.SendAsync(request, Ct);
    }

}
