namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>Closed list of suggestion types (data-model 4; member names equal the contract enum).</summary>
public enum SuggestionType
{
    /// <summary>spelling.typo.</summary>
    SpellingTypo,

    /// <summary>spelling.unknown-word.</summary>
    SpellingUnknownWord,

    /// <summary>spelling.capitalisation.</summary>
    SpellingCapitalisation,

    /// <summary>grammar.agreement.</summary>
    GrammarAgreement,

    /// <summary>grammar.verb-form.</summary>
    GrammarVerbForm,

    /// <summary>grammar.word-order.</summary>
    GrammarWordOrder,

    /// <summary>grammar.homophone.</summary>
    GrammarHomophone,

    /// <summary>grammar.other.</summary>
    GrammarOther,

    /// <summary>flow.stress.</summary>
    FlowStress,

    /// <summary>flow.awkward.</summary>
    FlowAwkward,

    /// <summary>flow.repetition.</summary>
    FlowRepetition,

    /// <summary>flow.clarity.</summary>
    FlowClarity,

    /// <summary>flow.syllable-count.</summary>
    FlowSyllableCount,

    /// <summary>licence.elision.</summary>
    LicenceElision,

    /// <summary>licence.inversion.</summary>
    LicenceInversion,

    /// <summary>licence.archaism.</summary>
    LicenceArchaism,

    /// <summary>licence.sound-spelling.</summary>
    LicenceSoundSpelling,

    /// <summary>licence.slang.</summary>
    LicenceSlang,

    /// <summary>licence.code-switch.</summary>
    LicenceCodeSwitch,

    /// <summary>device.end-rhyme.</summary>
    DeviceEndRhyme,

    /// <summary>device.internal-rhyme.</summary>
    DeviceInternalRhyme,

    /// <summary>device.assonance.</summary>
    DeviceAssonance,

    /// <summary>device.alliteration.</summary>
    DeviceAlliteration,

    /// <summary>device.imagery.</summary>
    DeviceImagery,

    /// <summary>device.syllable-fit.</summary>
    DeviceSyllableFit,

    /// <summary>device.rhythm.</summary>
    DeviceRhythm,

    /// <summary>write.alternative.</summary>
    WriteAlternative,

    /// <summary>write.continuation.</summary>
    WriteContinuation,

    /// <summary>write.title.</summary>
    WriteTitle,
}

/// <summary>Rules of the suggestion types.</summary>
public static class SuggestionTypes
{
    private static readonly (string Prefix, SuggestionCategory Category)[] Prefixes =
    [
        ("Spelling", SuggestionCategory.Spelling), ("Grammar", SuggestionCategory.Grammar), ("Flow", SuggestionCategory.Flow),
        ("Licence", SuggestionCategory.Licence), ("Device", SuggestionCategory.Device),
    ];

    /// <summary>The category of a type.</summary>
    /// <param name="type">Type.</param>
    /// <returns>The category.</returns>
    public static SuggestionCategory CategoryOf(SuggestionType type) => type switch
    {
        SuggestionType.WriteAlternative => SuggestionCategory.Alternative,
        SuggestionType.WriteContinuation => SuggestionCategory.Continuation,
        SuggestionType.WriteTitle => SuggestionCategory.Title,
        _ => Prefixes.First(entry => type.ToString().StartsWith(entry.Prefix, StringComparison.Ordinal)).Category,
    };

    /// <summary>True for corrections (spelling, grammar, flow), the categories that can be marked intentional.</summary>
    /// <param name="category">Category.</param>
    /// <returns>True for a correction.</returns>
    public static bool IsCorrection(SuggestionCategory category) =>
        category is SuggestionCategory.Spelling or SuggestionCategory.Grammar or SuggestionCategory.Flow;
}
