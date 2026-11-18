using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Scores;

/// <summary>Warning codes of a score (data-model.md 2.7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreWarningCodeDto>))]
public enum ScoreWarningCodeDto
{
    /// <summary>Tempo and bars come from the entered values.</summary>
    [JsonStringEnumMemberName("manual-tempo")]
    ManualTempo,

    /// <summary>The key estimate is uncertain.</summary>
    [JsonStringEnumMemberName("low-key-confidence")]
    LowKeyConfidence,

    /// <summary>Many short or out-of-range notes were removed.</summary>
    [JsonStringEnumMemberName("notes-cleaned")]
    NotesCleaned,

    /// <summary>Notes were left out in busy bars.</summary>
    [JsonStringEnumMemberName("dense-capped")]
    DenseCapped,

    /// <summary>The source is a detail stem (bleed).</summary>
    [JsonStringEnumMemberName("detail-bleed")]
    DetailBleed,

    /// <summary>The piano model ran on the CPU.</summary>
    [JsonStringEnumMemberName("cpu-piano")]
    CpuPiano,

    /// <summary>Very few notes were detected.</summary>
    [JsonStringEnumMemberName("few-notes")]
    FewNotes,

    /// <summary>A drum detail stem was missing.</summary>
    [JsonStringEnumMemberName("missing-drum-piece")]
    MissingDrumPiece,

    /// <summary>Notes were moved by an octave to fit the tuning (1.15.0).</summary>
    [JsonStringEnumMemberName("tab-octave-moved")]
    TabOctaveMoved,

    /// <summary>Chord notes were left out for lack of strings (1.15.0).</summary>
    [JsonStringEnumMemberName("tab-notes-dropped")]
    TabNotesDropped,

    /// <summary>Chords need a wide stretch (1.15.0).</summary>
    [JsonStringEnumMemberName("tab-wide-stretch")]
    TabWideStretch,

    /// <summary>Notes are outside the usual range of the written instrument (1.15.0).</summary>
    [JsonStringEnumMemberName("part-out-of-range")]
    PartOutOfRange,

    /// <summary>A section stem was reduced to its loudest line (1.15.0).</summary>
    [JsonStringEnumMemberName("section-reduced")]
    SectionReduced,
}
