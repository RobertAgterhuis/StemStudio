using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;

namespace StemStudio.Agent.Ports;

/// <summary>Asset library endpoints of the public API (features 007 exports, 008 scores) over loopback.</summary>
public interface IAssetActions
{
    /// <summary>GET /api/assets with filters (list_exports, list_scores).</summary>
    /// <param name="query">Filters.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Items, newest first, with the totals.</returns>
    Task<AssetListDto> ListAssetsAsync(AssetQuery query, CancellationToken cancellationToken);

    /// <summary>GET /api/assets/{assetId} (job status for wait_for_job).</summary>
    /// <param name="assetId">Item identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The item.</returns>
    Task<AssetDto> GetAssetAsync(Guid assetId, CancellationToken cancellationToken);

    /// <summary>POST /api/assets/audio-exports (export_audio).</summary>
    /// <param name="request">Export request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued item.</returns>
    Task<AssetDto> CreateAudioExportAsync(CreateAudioExportRequest request, CancellationToken cancellationToken);

    /// <summary>POST /api/assets/stem-packs (export_stem_pack).</summary>
    /// <param name="request">Stem pack request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued items.</returns>
    Task<StemPackDto> CreateStemPackAsync(CreateStemPackRequest request, CancellationToken cancellationToken);

    /// <summary>DELETE /api/assets/{assetId} (delete_export, delete_score; compensations of exports and scores).</summary>
    /// <param name="assetId">Item identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteAssetAsync(Guid assetId, CancellationToken cancellationToken);

    /// <summary>GET /api/scores/options?trackId=&amp;channel= (profiles and estimates for transcribe_stem).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="channel">Stem or detail channel id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The options.</returns>
    Task<ScoreOptionsDto> GetScoreOptionsAsync(Guid trackId, string channel, CancellationToken cancellationToken);

    /// <summary>POST /api/assets/scores (transcribe_stem, heavy).</summary>
    /// <param name="request">Score request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued item.</returns>
    Task<AssetDto> CreateScoreAsync(CreateScoreRequest request, CancellationToken cancellationToken);
}
