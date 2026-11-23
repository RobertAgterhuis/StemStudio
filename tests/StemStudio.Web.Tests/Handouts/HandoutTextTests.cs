using StemStudio.Contracts.Handouts;
using StemStudio.Web.Handouts;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Handouts;

/// <summary>Display texts of the handouts (data-model 6): every ingredient, action, transposition, estimate and summary.</summary>
public sealed class HandoutTextTests
{
    [Theory]
    [InlineData(IngredientCodeDto.Facts, "Song facts")]
    [InlineData(IngredientCodeDto.FormChart, "Form chart")]
    [InlineData(IngredientCodeDto.ChordsOverLyrics, "Lyrics with chords")]
    [InlineData(IngredientCodeDto.Lyrics, "Lyrics")]
    [InlineData(IngredientCodeDto.ChordChart, "Chord chart")]
    [InlineData(IngredientCodeDto.Notation, "Sheet music")]
    [InlineData(IngredientCodeDto.PracticeNotes, "Practice notes")]
    [InlineData(IngredientCodeDto.PracticeProgress, "Practice progress")]
    [InlineData(IngredientCodeDto.SetlistNotes, "Setlist notes")]
    public void Ingredients_HaveLabels(IngredientCodeDto code, string label) => HandoutText.Ingredient(code).ShouldBe(label);

    [Theory]
    [InlineData(IngredientActionDto.CreateScore, "Make sheet music")]
    [InlineData(IngredientActionDto.CreateTab, "Make tablature")]
    [InlineData(IngredientActionDto.CreateDrumNotation, "Make drum notation")]
    [InlineData(IngredientActionDto.CreateTransposedPart, "Make a transposed part")]
    [InlineData(IngredientActionDto.DetectChords, "Detect chords")]
    [InlineData(IngredientActionDto.DetectSections, "Find sections")]
    [InlineData(IngredientActionDto.FindLyrics, "Find lyrics")]
    [InlineData(IngredientActionDto.SplitDrums, "Split the drums")]
    [InlineData(IngredientActionDto.WriteNotes, "Write practice notes")]
    public void Actions_HaveLinkTexts(IngredientActionDto action, string text) => HandoutText.Action(action).ShouldBe(text);

    [Theory]
    [InlineData(1, "Takes about 1 second.")]
    [InlineData(0, "Takes about 1 second.")]
    [InlineData(45, "Takes about 45 seconds.")]
    [InlineData(90, "Takes about 2 minutes.")]
    public void Estimate_InSecondsOrMinutes(int seconds, string text) => HandoutText.Estimate(seconds).ShouldBe(text);

    [Fact]
    public void Summary_OfASetlistPartInBFlat_NamesTheTranspositionAndTheSongs()
    {
        var facts = InsightsData.Facts() with
        {
            Kind = HandoutKindDto.Setlist,
            Family = HandoutFamilyDto.Melodic,
            Transposition = TranspositionDto.BFlat,
            Paper = PaperDto.Letter,
            Pages = 1,
            Songs = 3,
        };

        HandoutText.Summary(facts).ShouldBe("Melodic instruments · 1 page · Letter · B-flat · 3 songs");
        HandoutText.Summary(facts with { Transposition = TranspositionDto.C, Kind = HandoutKindDto.Song }).ShouldBe("Melodic instruments · 1 page · Letter");
    }

    [Fact]
    public void Omitted_NamesEveryReason_OrNothing()
    {
        var facts = InsightsData.Facts() with
        {
            Omitted =
            [
                new OmittedIngredientDto { Code = IngredientCodeDto.Lyrics, Reason = OmissionReasonDto.NotSelected },
                new OmittedIngredientDto { Code = IngredientCodeDto.ChordsOverLyrics, Reason = OmissionReasonDto.NoTiming },
            ],
        };

        HandoutText.Omitted(facts).ShouldBe("Not included: lyrics (left out), lyrics with chords (no timings to place it)");
        HandoutText.Omitted(facts with { Omitted = [] }).ShouldBeNull();
    }

    [Fact]
    public void Transpositions_AndFamilies_HaveLabels()
    {
        Enum.GetValues<TranspositionDto>().Select(HandoutText.Transposition).ShouldBe(["C (concert pitch)", "B-flat", "E-flat", "F"]);
        HandoutText.Families.Select(HandoutText.Family).ShouldBe(["Band overview", "Vocals", "Guitar", "Bass", "Keys", "Drums", "Melodic instruments"]);
    }

    [Fact]
    public void Choices_KeepThePaper_AfterTheFirstOptions()
    {
        var choices = new HandoutChoices();
        choices.Reset(InsightsData.BassOptions(), first: true);
        choices.Paper = PaperDto.Letter;
        choices.SetNotation(InsightsData.BassScoreId, on: true);

        choices.Reset(InsightsData.Options(HandoutFamilyDto.Melodic, "Melodic instruments"), first: false);

        (choices.Paper, choices.Transposition).ShouldBe((PaperDto.Letter, TranspositionDto.C));
        choices.Selects(InsightsData.BassScoreId).ShouldBeTrue();
    }
}
