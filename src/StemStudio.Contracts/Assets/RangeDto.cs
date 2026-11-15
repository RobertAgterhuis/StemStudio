namespace StemStudio.Contracts.Assets;

/// <summary>Part of a track to export: <c>0 &lt;= start &lt; end &lt;= duration</c>, at least <see cref="ContractLimits.MinExportRangeSeconds"/> long.</summary>
/// <param name="StartSeconds">Start in seconds.</param>
/// <param name="EndSeconds">End in seconds.</param>
public sealed record RangeDto(double StartSeconds, double EndSeconds);
