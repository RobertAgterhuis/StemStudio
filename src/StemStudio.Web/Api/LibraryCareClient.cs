using System.Text.Json;
using System.Text.Json.Serialization;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;

namespace StemStudio.Web.Api;

/// <summary><see cref="HttpClient"/>-based <see cref="ILibraryCareClient"/>, registered as a typed client on the server-side API base URL.</summary>
/// <param name="http">The configured HTTP client.</param>
public sealed class LibraryCareClient(HttpClient http) : ILibraryCareClient
{
    /// <summary>
    /// JSON options of the contract: camelCase, nulls omitted when writing, and the report's <c>type</c> discriminator
    /// accepted anywhere in the object.
    /// </summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true,
    };

    private readonly ApiTransport _transport = new(http, Json);

    /// <inheritdoc />
    public Task<LibraryCareStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Get<LibraryCareStatusDto>(ApiRoutes.LibraryCareStatus, cancellationToken);

    /// <inheritdoc />
    public Task<BackupListDto> ListBackupsAsync(CancellationToken cancellationToken = default) =>
        Get<BackupListDto>(ApiRoutes.Backups, cancellationToken);

    /// <inheritdoc />
    public Task<BackupEstimateDto> EstimateBackupAsync(BackupOptionsDto options, CancellationToken cancellationToken = default) =>
        Get<BackupEstimateDto>(ApiRoutes.BackupEstimateFor(options), cancellationToken);

    /// <inheritdoc />
    public Task<LibraryJobDto> CreateBackupAsync(BackupOptionsDto options, CancellationToken cancellationToken = default) =>
        Post<LibraryJobDto>(ApiRoutes.Backups, options, cancellationToken);

    /// <inheritdoc />
    public Task<BackupDetailsDto> GetBackupAsync(string fileName, CancellationToken cancellationToken = default) =>
        Get<BackupDetailsDto>(ApiRoutes.Backup(fileName), cancellationToken);

    /// <inheritdoc />
    public Task DeleteBackupAsync(string fileName, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Delete, ApiRoutes.Backup(fileName), null, cancellationToken);

    /// <inheritdoc />
    public Task<LibraryJobDto> VerifyBackupAsync(string fileName, CancellationToken cancellationToken = default) =>
        Post<LibraryJobDto>(ApiRoutes.VerifyBackup(fileName), null, cancellationToken);

    /// <inheritdoc />
    public Task<LibraryJobDto> RestoreBackupAsync(string fileName, RestoreOptionsDto options, CancellationToken cancellationToken = default) =>
        Post<LibraryJobDto>(ApiRoutes.RestoreBackup(fileName), options, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<LibraryJobDto>> ListJobsAsync(int limit, CancellationToken cancellationToken = default) =>
        Get<IReadOnlyList<LibraryJobDto>>(ApiRoutes.LibraryJobsWithLimit(limit), cancellationToken);

    /// <inheritdoc />
    public Task<LibraryJobDto> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Get<LibraryJobDto>(ApiRoutes.LibraryJob(jobId), cancellationToken);

    /// <inheritdoc />
    public Task<LibraryJobDto> CancelJobAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        Post<LibraryJobDto>(ApiRoutes.CancelLibraryJob(jobId), null, cancellationToken);

    /// <inheritdoc />
    public Task<LibraryJobDto> RevertRestoreAsync(CancellationToken cancellationToken = default) =>
        Post<LibraryJobDto>(ApiRoutes.RevertRestore, null, cancellationToken);

    /// <inheritdoc />
    public Task DiscardPreviousLibraryAsync(string confirmation, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(HttpMethod.Post, ApiRoutes.DiscardPreviousLibrary, Body(new ConfirmationDto { Confirmation = confirmation }), cancellationToken);

    /// <inheritdoc />
    public Task<StorageOverviewDto> GetStorageOverviewAsync(bool refresh = false, CancellationToken cancellationToken = default) =>
        Get<StorageOverviewDto>(ApiRoutes.StorageOverview(refresh), cancellationToken);

    /// <inheritdoc />
    public Task<CleanupPlanDto> PreviewCleanupAsync(CleanupRequestDto request, CancellationToken cancellationToken = default) =>
        Post<CleanupPlanDto>(ApiRoutes.CleanupPreview, request, cancellationToken);

    /// <inheritdoc />
    public Task<CleanupResultDto> ExecuteCleanupAsync(Guid planId, CancellationToken cancellationToken = default) =>
        Post<CleanupResultDto>(ApiRoutes.Cleanup, new ExecuteCleanupRequestDto { PlanId = planId }, cancellationToken);

    private static JsonContent Body<T>(T value) => JsonContent.Create(value, options: Json);

    private Task<T> Get<T>(string uri, CancellationToken ct) => _transport.SendAsync<T>(HttpMethod.Get, uri, null, ct);

    private Task<T> Post<T>(string uri, object? body, CancellationToken ct) =>
        _transport.SendAsync<T>(HttpMethod.Post, uri, body is null ? null : JsonContent.Create(body, body.GetType(), options: Json), ct);
}
