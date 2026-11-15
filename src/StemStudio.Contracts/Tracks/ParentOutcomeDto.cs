namespace StemStudio.Contracts.Tracks;

/// <summary>Outcome of the detail phase for one standard stem.</summary>
/// <param name="Parent">Standard stem name, see <see cref="StemNames"/>.</param>
/// <param name="Outcome">What happened to the stem.</param>
/// <param name="Stems">Detail stems in display order with <c>rest</c> last; empty unless <paramref name="Outcome"/> is split.</param>
public sealed record ParentOutcomeDto(string Parent, ParentOutcomeKindDto Outcome, IReadOnlyList<DetailStemDto> Stems);
