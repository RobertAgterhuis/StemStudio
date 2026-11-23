using System.Globalization;
using StemStudio.Contracts.Handouts;

namespace StemStudio.Web.Handouts;

/// <summary>Display texts of the lesson handouts (feature 020, data-model 6; every family counts equally).</summary>
public static class HandoutText
{
    /// <summary>The seven families in menu order (data-model 6.1).</summary>
    public static IReadOnlyList<HandoutFamilyDto> Families { get; } =
    [
        HandoutFamilyDto.Overview, HandoutFamilyDto.Vocals, HandoutFamilyDto.Guitar, HandoutFamilyDto.Bass,
        HandoutFamilyDto.Keys, HandoutFamilyDto.Drums, HandoutFamilyDto.Melodic,
    ];

    /// <summary>"Band overview", "Melodic instruments", ...</summary>
    /// <param name="family">The family.</param>
    /// <returns>The label.</returns>
    public static string Family(HandoutFamilyDto family) => family switch
    {
        HandoutFamilyDto.Overview => "Band overview",
        HandoutFamilyDto.Melodic => "Melodic instruments",
        _ => Capital(Wire(family)),
    };

    /// <summary>"Form chart", "Sheet music", ...</summary>
    /// <param name="code">The ingredient.</param>
    /// <returns>The label.</returns>
    public static string Ingredient(IngredientCodeDto code) => code switch
    {
        IngredientCodeDto.Facts => "Song facts",
        IngredientCodeDto.FormChart => "Form chart",
        IngredientCodeDto.ChordsOverLyrics => "Lyrics with chords",
        IngredientCodeDto.Lyrics => "Lyrics",
        IngredientCodeDto.ChordChart => "Chord chart",
        IngredientCodeDto.Notation => "Sheet music",
        IngredientCodeDto.PracticeNotes => "Practice notes",
        IngredientCodeDto.PracticeProgress => "Practice progress",
        _ => "Setlist notes",
    };

    /// <summary>The create link text of a missing ingredient ("Make tablature").</summary>
    /// <param name="action">The action.</param>
    /// <returns>The text.</returns>
    public static string Action(IngredientActionDto action) => action switch
    {
        IngredientActionDto.CreateScore => "Make sheet music",
        IngredientActionDto.CreateTab => "Make tablature",
        IngredientActionDto.CreateDrumNotation => "Make drum notation",
        IngredientActionDto.CreateTransposedPart => "Make a transposed part",
        IngredientActionDto.DetectChords => "Detect chords",
        IngredientActionDto.DetectSections => "Find sections",
        IngredientActionDto.FindLyrics => "Find lyrics",
        IngredientActionDto.SplitDrums => "Split the drums",
        _ => "Write practice notes",
    };

    /// <summary>"C (concert pitch)", "B-flat", "E-flat", "F".</summary>
    /// <param name="transposition">The transposition.</param>
    /// <returns>The label.</returns>
    public static string Transposition(TranspositionDto transposition) => transposition switch
    {
        TranspositionDto.C => "C (concert pitch)",
        TranspositionDto.BFlat => "B-flat",
        TranspositionDto.EFlat => "E-flat",
        _ => "F",
    };

    /// <summary>"A4" or "Letter".</summary>
    /// <param name="paper">The paper.</param>
    /// <returns>The label.</returns>
    public static string Paper(PaperDto paper) => paper == PaperDto.A4 ? "A4" : "Letter";

    /// <summary>"Takes about 6 seconds." / "Takes about 2 minutes."</summary>
    /// <param name="seconds">The estimate.</param>
    /// <returns>The text.</returns>
    public static string Estimate(int seconds) => seconds < 90
        ? string.Create(CultureInfo.InvariantCulture, $"Takes about {Math.Max(1, seconds)} {(seconds == 1 ? "second" : "seconds")}.")
        : string.Create(CultureInfo.InvariantCulture, $"Takes about {(int)Math.Round(seconds / 60.0)} minutes.");

    /// <summary>The default ingredients that are missing or only partly there ("Practice notes: No notes yet").</summary>
    /// <param name="options">The options of the family.</param>
    /// <returns>One line per ingredient.</returns>
    public static IReadOnlyList<string> Missing(HandoutOptionsDto options) =>
    [
        .. options.Ingredients
            .Where(i => i.DefaultIncluded && (i.Status == IngredientStatusDto.Missing || i.Action is not null))
            .Select(i => $"{Ingredient(i.Code)}: {i.Text}"),
    ];

    /// <summary>"Not included: practice notes (not available yet), lyrics (left out)"; null when nothing was left out.</summary>
    /// <param name="facts">The facts of a ready handout.</param>
    /// <returns>The text or null.</returns>
    public static string? Omitted(HandoutFactsDto facts) => facts.Omitted.Count == 0
        ? null
        : "Not included: " + string.Join(", ", facts.Omitted.Select(o => $"{Lower(Ingredient(o.Code))} ({Reason(o.Reason)})"));

    /// <summary>"Bass · 4 pages · A4" (+ " · B-flat" and " · 3 songs" when they apply).</summary>
    /// <param name="facts">The facts of a ready handout.</param>
    /// <returns>The line.</returns>
    public static string Summary(HandoutFactsDto facts)
    {
        var parts = new List<string> { Family(facts.Family), $"{facts.Pages} {(facts.Pages == 1 ? "page" : "pages")}", Paper(facts.Paper) };
        parts.AddRange(facts.Transposition is { } t and not TranspositionDto.C ? [Transposition(t)] : []);
        parts.AddRange(facts.Kind == HandoutKindDto.Setlist ? [$"{facts.Songs} songs"] : []);
        return string.Join(" · ", parts);
    }

    /// <summary>The wire value of a family (<c>overview</c>, <c>melodic</c>), used in ids and query strings.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The value.</returns>
    public static string Wire(HandoutFamilyDto family) => StemStudio.Web.Services.WireName.Of(family);

    private static string Reason(OmissionReasonDto reason) => reason switch
    {
        OmissionReasonDto.NotAvailable => "not available yet",
        OmissionReasonDto.NotSelected => "left out",
        _ => "no timings to place it",
    };

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    private static string Lower(string text) => char.ToLowerInvariant(text[0]) + text[1..];
}
