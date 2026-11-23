using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Contracts.Insights;

namespace StemStudio.Web.Api;

/// <summary>Stem quality report, lesson handouts and practice notes (feature 020, backend contract 1.19.0).</summary>
public sealed partial class StemStudioApiClient
{
    /// <inheritdoc />
    public Task<QualityReportDto> GetQualityReportAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<QualityReportDto> RequestQualityReportAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<HandoutOptionsDto> GetHandoutOptionsAsync(
        HandoutSourceDto source, HandoutFamilyDto family, TranspositionDto? transposition = null, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<AssetDto> CreateHandoutAsync(CreateHandoutRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<PracticeNotesDto> GetPracticeNotesAsync(Guid trackId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<PracticeNoteDto?> SetPracticeNoteAsync(Guid trackId, HandoutFamilyDto family, string text, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
