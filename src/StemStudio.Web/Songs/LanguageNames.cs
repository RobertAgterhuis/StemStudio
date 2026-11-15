using StemStudio.Contracts;

namespace StemStudio.Web.Songs;

/// <summary>English names of the lyrics languages (<see cref="ContractLimits.LyricsLanguages"/>), independent of the server's ICU data.</summary>
public static class LanguageNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["ar"] = "Arabic", ["az"] = "Azerbaijani", ["bg"] = "Bulgarian", ["bn"] = "Bengali", ["ca"] = "Catalan", ["cs"] = "Czech",
        ["da"] = "Danish", ["de"] = "German", ["el"] = "Greek", ["en"] = "English", ["es"] = "Spanish", ["fa"] = "Persian",
        ["fi"] = "Finnish", ["fr"] = "French", ["he"] = "Hebrew", ["hi"] = "Hindi", ["hr"] = "Croatian", ["ht"] = "Haitian Creole",
        ["hu"] = "Hungarian", ["id"] = "Indonesian", ["is"] = "Icelandic", ["it"] = "Italian", ["ja"] = "Japanese", ["ko"] = "Korean",
        ["la"] = "Latin", ["lt"] = "Lithuanian", ["ms"] = "Malay", ["ne"] = "Nepali", ["nl"] = "Dutch", ["no"] = "Norwegian",
        ["pa"] = "Punjabi", ["pl"] = "Polish", ["pt"] = "Portuguese", ["ro"] = "Romanian", ["ru"] = "Russian", ["sa"] = "Sanskrit",
        ["sk"] = "Slovak", ["sr"] = "Serbian", ["sv"] = "Swedish", ["sw"] = "Swahili", ["ta"] = "Tamil", ["te"] = "Telugu",
        ["th"] = "Thai", ["tl"] = "Tagalog", ["tr"] = "Turkish", ["uk"] = "Ukrainian", ["ur"] = "Urdu", ["vi"] = "Vietnamese",
        ["yue"] = "Cantonese", ["zh"] = "Chinese", ["unknown"] = "Other or unknown",
    };

    /// <summary>Every language code of the contract, in contract order.</summary>
    public static IReadOnlyList<string> Codes => ContractLimits.LyricsLanguages;

    /// <summary>The English name of a code; unknown codes are shown as they are.</summary>
    /// <param name="code">For example <c>nl</c>.</param>
    /// <returns>For example "Dutch".</returns>
    public static string Of(string code) => Names.GetValueOrDefault(code, code);
}
