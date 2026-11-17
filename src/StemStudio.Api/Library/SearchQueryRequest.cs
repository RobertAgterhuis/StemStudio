using StemStudio.Contracts;
using StemStudio.Domain;
using StemStudio.Domain.Library;

namespace StemStudio.Api.Library;

/// <summary>Query string of the live search (backend-api 1.22.0 <c>searchLibrary</c>).</summary>
/// <param name="Q">The search text (1..100 characters after trimming, at least one letter or digit).</param>
/// <param name="Within">Only inside this folder.</param>
/// <param name="Limit">Hits per group (1..200, default 20).</param>
public sealed record SearchQueryRequest(string? Q, string? Within, int? Limit)
{
    /// <summary>Validates the request.</summary>
    /// <param name="errors">Collects the violated rules (field = parameter name).</param>
    /// <returns>The request (valid only when no error was added).</returns>
    public SearchRequest ToRequest(List<DomainError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var text = Q?.Trim() ?? string.Empty;
        var limit = Limit ?? ContractLimits.LibrarySearchGroupDefault;
        Check(errors, text.Length is > 0 and <= ContractLimits.LibrarySearchMaxLength, "q", $"Type 1 to {ContractLimits.LibrarySearchMaxLength} characters.");
        Check(errors, text.Length == 0 || LibraryKey.IsMeaningful(text), "q", "Type at least one letter or digit.");
        Check(errors, Within is null || FolderId.TryParse(Within, out _), "within", "Not a folder id (ar, al or so followed by 16 hex characters).");
        Check(errors, limit is >= 1 and <= ContractLimits.LibrarySearchGroupMax, "limit", $"Must be between 1 and {ContractLimits.LibrarySearchGroupMax}.");
        return new SearchRequest(text, errors.Count == 0 ? LibrarySearch.Tokenize(text) : [], Within, limit);
    }

    private static void Check(List<DomainError> errors, bool valid, string field, string message)
    {
        if (!valid)
        {
            errors.Add(new DomainError(field, message));
        }
    }
}
