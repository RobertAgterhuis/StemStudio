using System.Globalization;
using System.Security.Cryptography;
using StemStudio.Api.Generations;
using StemStudio.Api.Generator;
using StemStudio.Api.Storage;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Voices;
using StemStudio.Domain;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Voices;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Voices;

/// <summary>The fields of <c>POST /api/voice-profiles</c>.</summary>
/// <param name="Sample">The sample bytes.</param>
/// <param name="FileName">The sample file name.</param>
/// <param name="DisplayName">Profile name.</param>
/// <param name="PersonName">Typed name of the person.</param>
/// <param name="ConsentConfirmed">The consent checkbox.</param>
/// <param name="ConsentStatementVersion">The statement version shown.</param>
/// <param name="Source">recorded or uploaded.</param>
public sealed record VoiceProfileRequest(
    byte[] Sample, string FileName, string? DisplayName, string? PersonName, bool ConsentConfirmed, string? ConsentStatementVersion, string? Source);

/// <summary>Voice profiles with consent (010 FR-005..FR-007, ADR-019 D3): consent first, nothing stored until every check passed.</summary>
/// <param name="profiles">Profile repository.</param>
/// <param name="generations">Generation repository (converted take counts).</param>
/// <param name="singer">The singer client.</param>
/// <param name="availability">Singer availability.</param>
/// <param name="hashes">The exact-copy guard.</param>
/// <param name="data">The data directory.</param>
/// <param name="clock">Clock.</param>
public sealed class VoiceProfileService(
    IVoiceProfileRepository profiles,
    IGenerationRepository generations,
    ISingerClient singer,
    SingerAvailability availability,
    AudioHashIndex hashes,
    DataDirectory data,
    TimeProvider clock)
{
    /// <summary>Largest sample (20 MiB).</summary>
    public const int MaxSampleBytes = 20 * 1024 * 1024;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Creates a profile after the consent, name, singer, exact-copy and sample checks.</summary>
    /// <param name="request">The form fields.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>201, 400, 409, 413, 422 or 503.</returns>
    public async Task<IResult> CreateAsync(VoiceProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var consent = ConsentRules.Check(request.ConsentConfirmed, request.ConsentStatementVersion, request.PersonName, Now);
        if (consent.Consent is null)
        {
            return VoiceProblems.Consent(consent);
        }

        var (source, problem) = await FormProblemAsync(request, cancellationToken);
        return problem ?? await CheckSampleAsync(request, new VoiceProfileDraft(request.DisplayName!.Trim(), consent.Consent, Placeholder(source)), cancellationToken);
    }

    /// <summary>Every profile, by name.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200.</returns>
    public async Task<IResult> ListAsync(CancellationToken cancellationToken)
    {
        var dtos = new List<VoiceProfileDto>();
        foreach (var profile in await profiles.ListAsync(cancellationToken))
        {
            dtos.Add(await ToDtoAsync(profile, cancellationToken));
        }

        return TypedResults.Ok(dtos);
    }

    /// <summary>One profile.</summary>
    /// <param name="id">Profile id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await profiles.FindAsync(id, cancellationToken) is { } profile ? TypedResults.Ok(await ToDtoAsync(profile, cancellationToken)) : VoiceProblems.NotFound(id);

    /// <summary>Serves the normalised sample.</summary>
    /// <param name="id">Profile id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The file or 404.</returns>
    public async Task<IResult> GetSampleAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await profiles.FindAsync(id, cancellationToken);
        var path = profile is null ? null : data.ContainedPath(profile.SamplePath);
        return StemService.ServeFile(path is not null && File.Exists(path) ? new FileInfo(path) : null, $"Voice profile {id} has no sample.");
    }

    /// <summary>The current consent statement.</summary>
    /// <returns>200.</returns>
    public static IResult Statement() =>
        TypedResults.Ok(new ConsentStatementDto { Version = ConsentStatement.Version, Text = ConsentStatement.Text, TextNl = ConsentStatement.TextNl });

    private async Task<(VoiceSampleSource Source, IResult? Problem)> FormProblemAsync(VoiceProfileRequest request, CancellationToken cancellationToken)
    {
        var name = (request.DisplayName ?? string.Empty).Trim();
        var sourceValid = Enum.TryParse<VoiceSampleSource>(request.Source, true, out var source) && Enum.IsDefined(source);
        var errors = new List<DomainError>();
        AddIf(errors, name.Length is 0 or > VoiceProfile.MaxDisplayNameLength, "displayName", "The name must be 1 to 60 characters.");
        AddIf(errors, !sourceValid, "source", "Use recorded or uploaded.");
        if (errors.Count > 0)
        {
            return (source, ApiProblems.Validation(errors, string.Empty));
        }

        return (source, await profiles.IsNameTakenAsync(VoiceNaming.Normalize(name), cancellationToken) ? VoiceProblems.NameTaken(name) : await SingerProblemAsync(request, cancellationToken));
    }

    private static void AddIf(List<DomainError> errors, bool invalid, string field, string message)
    {
        if (invalid)
        {
            errors.Add(new DomainError(field, message));
        }
    }

    private async Task<IResult?> SingerProblemAsync(VoiceProfileRequest request, CancellationToken cancellationToken)
    {
        var readiness = await availability.GetAsync(cancellationToken);
        if (!readiness.IsReady)
        {
            return VoiceProblems.NotReady(readiness);
        }

        if (request.Sample.Length > MaxSampleBytes)
        {
            return VoiceProblems.SampleTooLarge();
        }

        return await hashes.ContainsAsync(Convert.ToHexStringLower(SHA256.HashData(request.Sample)), cancellationToken) ? VoiceProblems.LibraryAudio() : null;
    }

    private async Task<IResult> CheckSampleAsync(VoiceProfileRequest request, VoiceProfileDraft draft, CancellationToken cancellationToken)
    {
        try
        {
            await using var sample = new MemoryStream(request.Sample);
            var check = await singer.CheckVoiceSampleAsync(sample, request.FileName, cancellationToken);
            return check.Ok ? await StoreAsync(request, draft, check, cancellationToken) : VoiceProblems.Rejected(check);
        }
        catch (Exception exception) when (exception is GeneratorUnavailableException or GeneratorRequestException)
        {
            return GenerationProblems.Unavailable();
        }
    }

    private async Task<IResult> StoreAsync(VoiceProfileRequest request, VoiceProfileDraft draft, GeneratorSampleCheck check, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var relative = string.Create(CultureInfo.InvariantCulture, $"voices/{id:D}/sample.flac");
        var path = data.RequireContainedPath(relative);
        await AtomicFile.SaveAsync(path, (stream, ct) => singer.DownloadCheckedSampleAsync(check.CheckId, stream, ct), cancellationToken);
        var sample = draft.Sample with
        {
            FilePath = relative,
            PcmSha256 = check.PcmSha256,
            FileSha256 = Convert.ToHexStringLower(SHA256.HashData(request.Sample)),
            DurationSeconds = check.DurationSeconds,
            VoicedRatio = check.VoicedRatio,
        };
        var profile = VoiceProfile.Create(id, draft with { Sample = sample }, Now);
        await profiles.AddAsync(profile, cancellationToken);
        return TypedResults.Created(ApiRoutes.VoiceProfile(id), await ToDtoAsync(profile, cancellationToken));
    }

    private static VoiceSample Placeholder(VoiceSampleSource source) => new(source, string.Empty, string.Empty, string.Empty, 0, 0);

    private async Task<VoiceProfileDto> ToDtoAsync(VoiceProfile profile, CancellationToken cancellationToken)
    {
        var converted = (await generations.ListByVoiceProfileAsync(profile.Id, cancellationToken)).Count(generation => generation.Status == GenerationState.Completed);
        return new VoiceProfileDto
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Consent = new VoiceConsentDto { StatementVersion = profile.Consent.StatementVersion, PersonName = profile.Consent.PersonName, ConsentedAt = TrackMapper.Utc(profile.Consent.ConsentedAt) },
            Source = Enum.Parse<VoiceSampleSourceDto>(profile.SampleSource.ToString()),
            DurationSeconds = profile.DurationSeconds,
            VoicedRatio = profile.VoicedRatio,
            SampleUrl = ApiRoutes.VoiceSample(profile.Id),
            CreatedAt = TrackMapper.Utc(profile.CreatedAt),
            ConvertedTakeCount = converted,
        };
    }
}
