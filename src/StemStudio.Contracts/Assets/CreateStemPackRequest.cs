namespace StemStudio.Contracts.Assets;

/// <summary>Body of <c>POST /api/assets/stem-packs</c>: one raw item per stem (backend contract 1.5.0).</summary>
/// <param name="TrackId">A completed track.</param>
/// <param name="Format">Output format of every item.</param>
/// <param name="IncludeDetail">Also export every kept detail stem of split parents.</param>
public sealed record CreateStemPackRequest(Guid TrackId, ExportFormatDto Format, bool IncludeDetail = false);
