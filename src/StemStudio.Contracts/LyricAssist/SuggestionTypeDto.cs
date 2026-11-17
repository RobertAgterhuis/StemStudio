using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Closed list of suggestion types (schema <c>SuggestionType</c>, data-model section 4); the prefix is the category.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuggestionTypeDto>))]
public enum SuggestionTypeDto
{
    /// <summary><c>spelling.typo</c>.</summary>
    [JsonStringEnumMemberName("spelling.typo")]
    SpellingTypo,

    /// <summary><c>spelling.unknown-word</c>.</summary>
    [JsonStringEnumMemberName("spelling.unknown-word")]
    SpellingUnknownWord,

    /// <summary><c>spelling.capitalisation</c>.</summary>
    [JsonStringEnumMemberName("spelling.capitalisation")]
    SpellingCapitalisation,

    /// <summary><c>grammar.agreement</c>.</summary>
    [JsonStringEnumMemberName("grammar.agreement")]
    GrammarAgreement,

    /// <summary><c>grammar.verb-form</c>.</summary>
    [JsonStringEnumMemberName("grammar.verb-form")]
    GrammarVerbForm,

    /// <summary><c>grammar.word-order</c>.</summary>
    [JsonStringEnumMemberName("grammar.word-order")]
    GrammarWordOrder,

    /// <summary><c>grammar.homophone</c>.</summary>
    [JsonStringEnumMemberName("grammar.homophone")]
    GrammarHomophone,

    /// <summary><c>grammar.other</c>.</summary>
    [JsonStringEnumMemberName("grammar.other")]
    GrammarOther,

    /// <summary><c>flow.stress</c>.</summary>
    [JsonStringEnumMemberName("flow.stress")]
    FlowStress,

    /// <summary><c>flow.awkward</c>.</summary>
    [JsonStringEnumMemberName("flow.awkward")]
    FlowAwkward,

    /// <summary><c>flow.repetition</c>.</summary>
    [JsonStringEnumMemberName("flow.repetition")]
    FlowRepetition,

    /// <summary><c>flow.clarity</c>.</summary>
    [JsonStringEnumMemberName("flow.clarity")]
    FlowClarity,

    /// <summary><c>flow.syllable-count</c>.</summary>
    [JsonStringEnumMemberName("flow.syllable-count")]
    FlowSyllableCount,

    /// <summary><c>licence.elision</c>.</summary>
    [JsonStringEnumMemberName("licence.elision")]
    LicenceElision,

    /// <summary><c>licence.inversion</c>.</summary>
    [JsonStringEnumMemberName("licence.inversion")]
    LicenceInversion,

    /// <summary><c>licence.archaism</c>.</summary>
    [JsonStringEnumMemberName("licence.archaism")]
    LicenceArchaism,

    /// <summary><c>licence.sound-spelling</c>.</summary>
    [JsonStringEnumMemberName("licence.sound-spelling")]
    LicenceSoundSpelling,

    /// <summary><c>licence.slang</c>.</summary>
    [JsonStringEnumMemberName("licence.slang")]
    LicenceSlang,

    /// <summary><c>licence.code-switch</c>.</summary>
    [JsonStringEnumMemberName("licence.code-switch")]
    LicenceCodeSwitch,

    /// <summary><c>device.end-rhyme</c>.</summary>
    [JsonStringEnumMemberName("device.end-rhyme")]
    DeviceEndRhyme,

    /// <summary><c>device.internal-rhyme</c>.</summary>
    [JsonStringEnumMemberName("device.internal-rhyme")]
    DeviceInternalRhyme,

    /// <summary><c>device.assonance</c>.</summary>
    [JsonStringEnumMemberName("device.assonance")]
    DeviceAssonance,

    /// <summary><c>device.alliteration</c>.</summary>
    [JsonStringEnumMemberName("device.alliteration")]
    DeviceAlliteration,

    /// <summary><c>device.imagery</c>.</summary>
    [JsonStringEnumMemberName("device.imagery")]
    DeviceImagery,

    /// <summary><c>device.syllable-fit</c>.</summary>
    [JsonStringEnumMemberName("device.syllable-fit")]
    DeviceSyllableFit,

    /// <summary><c>device.rhythm</c>.</summary>
    [JsonStringEnumMemberName("device.rhythm")]
    DeviceRhythm,

    /// <summary><c>write.alternative</c>.</summary>
    [JsonStringEnumMemberName("write.alternative")]
    WriteAlternative,

    /// <summary><c>write.continuation</c>.</summary>
    [JsonStringEnumMemberName("write.continuation")]
    WriteContinuation,

    /// <summary><c>write.title</c>.</summary>
    [JsonStringEnumMemberName("write.title")]
    WriteTitle,
}
