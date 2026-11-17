namespace StemStudio.Contracts.LyricAssist;

/// <summary>Limits and fixed values of the lyric-assistant contract (backend-api 1.23.0, data-model, research R-03 - R-07).</summary>
public static class LyricAssistLimits
{
    /// <summary>Suggestions per assist and per sheet list.</summary>
    public const int MaxSuggestions = 400;

    /// <summary>Assists listed (and kept) per sheet, newest first.</summary>
    public const int MaxAssistsPerSheet = 20;

    /// <summary>Suggestion ids per accept or reject request; also <c>AssistProblem.suggestionIds</c>.</summary>
    public const int MaxDecisionIds = 100;

    /// <summary>Categories in one assist request.</summary>
    public const int MaxCategories = 5;

    /// <summary>Length of an instruction (mood or theme hint).</summary>
    public const int InstructionMaxLength = 200;

    /// <summary>Length of an explanation.</summary>
    public const int ExplanationMaxLength = 300;

    /// <summary>Length of a proposal and of an anchor's original text.</summary>
    public const int ProposalMaxLength = 400;

    /// <summary>Lines one continuation or insertion may add.</summary>
    public const int MaxInsertedLines = 4;

    /// <summary>Smallest count of alternatives, continuation lines and titles.</summary>
    public const int MinCount = 1;

    /// <summary>Most alternatives of one line.</summary>
    public const int MaxAlternatives = 5;

    /// <summary>Default number of alternatives.</summary>
    public const int DefaultAlternatives = 3;

    /// <summary>Most continuation lines.</summary>
    public const int MaxContinuationLines = 4;

    /// <summary>Default number of continuation lines.</summary>
    public const int DefaultContinuationLines = 2;

    /// <summary>Most title ideas.</summary>
    public const int MaxTitles = 5;

    /// <summary>Default number of title ideas.</summary>
    public const int DefaultTitles = 5;

    /// <summary>Intentional marks per sheet.</summary>
    public const int MaxIntentionalMarks = 200;

    /// <summary>Length of a <c>word</c> mark (no spaces).</summary>
    public const int MarkWordMaxLength = 40;

    /// <summary>Length of a <c>phrase</c> mark.</summary>
    public const int MarkPhraseMaxLength = 200;

    /// <summary>Length of a <c>line</c> mark.</summary>
    public const int MarkLineMaxLength = 400;

    /// <summary>Length of a mark note.</summary>
    public const int NoteMaxLength = 120;

    /// <summary>Personal words per language.</summary>
    public const int MaxWordsPerLanguage = 5000;

    /// <summary>Length of a personal word, a rhyme word and a rhyme partner.</summary>
    public const int WordMaxLength = 40;

    /// <summary>Default number of rhymes returned.</summary>
    public const int DefaultRhymes = 30;

    /// <summary>Most rhymes returned.</summary>
    public const int MaxRhymes = 60;

    /// <summary>Revisions kept per sheet; also the length of the revision list.</summary>
    public const int RevisionRetention = 50;

    /// <summary>Raw text accepted by the syllable and language-guess endpoints (normalised to the 4,096-character sheet limit).</summary>
    public const int RawTextMaxLength = 65536;

    /// <summary>Lowest tempo of a rhythm target.</summary>
    public const int MinBpm = 40;

    /// <summary>Highest tempo of a rhythm target.</summary>
    public const int MaxBpm = 220;

    /// <summary>Fewest beats per bar.</summary>
    public const int MinBeatsPerBar = 2;

    /// <summary>Most beats per bar.</summary>
    public const int MaxBeatsPerBar = 7;

    /// <summary>Fewest bars per line.</summary>
    public const int MinBarsPerLine = 1;

    /// <summary>Most bars per line.</summary>
    public const int MaxBarsPerLine = 8;

    /// <summary>Fewest syllables per beat.</summary>
    public const double MinSyllablesPerBeat = 0.5;

    /// <summary>Most syllables per beat.</summary>
    public const double MaxSyllablesPerBeat = 4;

    /// <summary>Step of syllables per beat.</summary>
    public const double SyllablesPerBeatStep = 0.25;

    /// <summary>A line deviates when it is more than this many syllables from the target or the section median.</summary>
    public const int SyllableDeviation = 2;

    /// <summary>Words per second above which the UI warns (010 density).</summary>
    public const double DensityWarnWordsPerSecond = 4.0;

    /// <summary>A language guess needs at least this share of dictionary hits.</summary>
    public const double LanguageGuessMinHitRate = 0.6;

    /// <summary>Length of the own-work confirmer's name.</summary>
    public const int ConfirmedByMaxLength = 80;

    /// <summary>Length of a statement version.</summary>
    public const int StatementVersionMaxLength = 40;

    /// <summary>Length of a matched track title.</summary>
    public const int MatchedTrackTitleMaxLength = 200;

    /// <summary>Current version of the lyric-assistant Claude notice.</summary>
    public const string ClaudeNoticeVersion = "lyric-claude-notice.v1";

    /// <summary>Current version of the own-work statement (owner decision Q1).</summary>
    public const string OwnWorkStatementVersion = "own-work.v1";

    /// <summary>Default lines per model block (configurable, <c>StemStudio__LyricAssist__BlockLines</c>; Phase 0 spike T008).</summary>
    public const int DefaultBlockLines = 8;

    /// <summary>Default Claude input token cap per assist (<c>StemStudio__LyricAssist__MaxInputTokensPerAssist</c>).</summary>
    public const int DefaultMaxInputTokensPerAssist = 20000;

    /// <summary>Default Claude output token cap per assist (<c>StemStudio__LyricAssist__MaxOutputTokensPerAssist</c>).</summary>
    public const int DefaultMaxOutputTokensPerAssist = 4000;

    /// <summary>Words in a verbatim shingle (research R-07).</summary>
    public const int VerbatimShingleWords = 7;

    /// <summary>Words a line needs to count as a full-line match (research R-07).</summary>
    public const int VerbatimLineMinWords = 5;

    /// <summary>Sheet guard: matching lines needed (and <see cref="NotOwnMinLineShare"/>).</summary>
    public const int NotOwnMinLines = 3;

    /// <summary>Sheet guard: share of the non-marker lines that must match.</summary>
    public const double NotOwnMinLineShare = 0.25;

    /// <summary>Seconds between SSE keep-alive comments.</summary>
    public const int KeepAliveSeconds = 15;

    /// <summary>Largest stored event payload in bytes.</summary>
    public const int EventPayloadMaxBytes = 16 * 1024;
}
