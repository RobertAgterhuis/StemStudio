using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Sections;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Tests.Practice;

/// <summary>T048: current / next / previous / restart and loop-section rules of 014 data-model 5.3 (FR-004).</summary>
public sealed class SectionNavigatorTests
{
    private static readonly SectionDto Intro = Section(SectionKindDto.Intro, "Intro", 0, 10);
    private static readonly SectionDto Verse = Section(SectionKindDto.Verse, "Verse 1", 10, 30);
    private static readonly SectionDto Chorus = Section(SectionKindDto.Chorus, "Chorus 1", 30, 50);
    private static readonly IReadOnlyList<SectionDto> Song = [Intro, Verse, Chorus];

    internal static SectionDto Section(SectionKindDto kind, string label, double start, double end) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        Label = label,
        StartSeconds = start,
        EndSeconds = end,
        Instrument = null,
        Confidence = 0.8,
        Origin = SectionOriginDto.Detected,
    };

    [Theory]
    [InlineData(0, "Intro")]
    [InlineData(9.99, "Intro")]
    [InlineData(10, "Verse 1")]
    [InlineData(49, "Chorus 1")]
    [InlineData(70, "Chorus 1")]
    public void Current_IsTheLastSectionStartingAtOrBeforeThePosition(double position, string label)
    {
        SectionNavigator.Current(Song, position)!.Label.ShouldBe(label);
    }

    [Fact]
    public void Current_IsNoneBeforeTheFirstSectionOrWithoutSections()
    {
        SectionNavigator.Current([Verse], 5).ShouldBeNull();
        SectionNavigator.Current([], 5).ShouldBeNull();
    }

    [Theory]
    [InlineData(0, "Verse 1")]
    [InlineData(9.96, "Verse 1")]
    [InlineData(9.97, "Chorus 1")]
    [InlineData(29.5, "Chorus 1")]
    public void Next_IsTheFirstSectionStartingMoreThan50msAhead(double position, string label)
    {
        SectionNavigator.Next(Song, position)!.Label.ShouldBe(label);
    }

    [Fact]
    public void Next_IsNoneInTheLastSection()
    {
        SectionNavigator.Next(Song, 40).ShouldBeNull();
    }

    [Theory]
    [InlineData(12.5, "Verse 1")]
    [InlineData(11.9, "Intro")]
    [InlineData(12, "Intro")]
    [InlineData(1, null)]
    [InlineData(5, "Intro")]
    public void Previous_RestartsTheCurrentAfter2SecondsElseGoesBack(double position, string? label)
    {
        SectionNavigator.Previous(Song, position)?.Label.ShouldBe(label);
        (SectionNavigator.Previous(Song, position) is null).ShouldBe(label is null);
    }

    [Theory]
    [InlineData(15, 10)]
    [InlineData(10, 10)]
    [InlineData(3, 0)]
    public void Restart_IsTheCurrentSectionStart(double position, double expected)
    {
        SectionNavigator.RestartPosition(Song, position).ShouldBe(expected);
        SectionNavigator.RestartPosition([Verse], 4).ShouldBe(0);
    }

    [Fact]
    public void LoopOf_LoopsTheWholeSectionAndRemembersIt()
    {
        SectionNavigator.LoopOf(Verse).ShouldBe(new LoopRegionDto(10, 30, true, Verse.Id));
    }

    [Fact]
    public void NextJump_WithoutALoop_SeeksAndLeavesTheLoop()
    {
        var jump = SectionNavigator.NextJump(Song, 12, null)!;

        jump.ShouldBe(new SectionJump(30, null, Chorus));
    }

    [Fact]
    public void NextJump_InsideAnActiveLoop_MovesTheLoopToTheNextSection()
    {
        var jump = SectionNavigator.NextJump(Song, 20, SectionNavigator.LoopOf(Verse))!;

        jump.ShouldBe(new SectionJump(30, SectionNavigator.LoopOf(Chorus), Chorus));
    }

    [Fact]
    public void NextJump_UsesTheLoopSectionEvenWhenThePositionIsAtItsEnd()
    {
        var hand = new LoopRegionDto(12, 20, true);

        SectionNavigator.NextJump(Song, 29.99, SectionNavigator.LoopOf(Verse))!.Section.ShouldBe(Chorus);
        SectionNavigator.NextJump(Song, 15, hand)!.Loop.ShouldBe(SectionNavigator.LoopOf(Chorus));
        SectionNavigator.NextJump(Song, 25, hand)!.Loop.ShouldBeNull();
        SectionNavigator.NextJump(Song, 15, hand with { Enabled = false })!.Loop.ShouldBeNull();
    }

    [Fact]
    public void NextJump_IsNoneAfterTheLastSection()
    {
        SectionNavigator.NextJump(Song, 40, null).ShouldBeNull();
        SectionNavigator.NextJump(Song, 40, SectionNavigator.LoopOf(Chorus)).ShouldBeNull();
    }

    [Fact]
    public void PreviousJump_InsideAnActiveLoop_MovesTheLoopBack()
    {
        SectionNavigator.PreviousJump(Song, 35, SectionNavigator.LoopOf(Chorus)).ShouldBe(new SectionJump(10, SectionNavigator.LoopOf(Verse), Verse));
        SectionNavigator.PreviousJump(Song, 35, null).ShouldBe(new SectionJump(30, null, Chorus));
        SectionNavigator.PreviousJump(Song, 1, null).ShouldBeNull();
        SectionNavigator.PreviousJump(Song, 5, SectionNavigator.LoopOf(Intro)).ShouldBeNull();
    }

    [Fact]
    public void Arguments_AreChecked()
    {
        Should.Throw<ArgumentNullException>(() => SectionNavigator.Current(null!, 0));
        Should.Throw<ArgumentNullException>(() => SectionNavigator.LoopOf(null!));
    }
}
