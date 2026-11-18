using StemStudio.Domain.Practice;
using static StemStudio.Domain.Tests.Practice.PracticeRulesTests;

namespace StemStudio.Domain.Tests.Practice;

/// <summary>The practice summary computed on read (014 FR-027, data-model 3.8).</summary>
public sealed class PracticeSummaryCalculatorTests
{
    private static readonly Guid Solo = Guid.NewGuid();

    private static PracticeSession Stored(DateTime start, double minutes, double played, params PracticeSectionInput[] sections) =>
        PracticeSession.Create(Guid.NewGuid(), Session(start, minutes, played, sections), start.AddMinutes(minutes));

    private static PracticeSummary Summarize(int days, int offset, params PracticeSession[] sessions) =>
        PracticeSummaryCalculator.Summarize(sessions, new SummaryWindow(Now, days, offset), new Dictionary<Guid, string>());

    [Fact]
    public void NoSessions_GiveZeros_AndOneEmptyEntryPerDay()
    {
        var summary = Summarize(30, 0);

        (summary.TodaySeconds, summary.Last7DaysSeconds, summary.TotalSeconds, summary.Sessions, summary.LastPractisedAt).ShouldBe((0.0, 0.0, 0.0, 0, (DateTime?)null));
        summary.Days.Count.ShouldBe(30);
        summary.Days[^1].ShouldBe(new PracticeDay(new DateOnly(2026, 11, 16), 0));
        summary.Days[0].Date.ShouldBe(new DateOnly(2026, 10, 18));
        summary.Sections.ShouldBeEmpty();
        summary.Tempo.ShouldBeEmpty();
    }

    [Fact]
    public void Totals_TodayLast7DaysAndTotal()
    {
        var today = Stored(Now.AddMinutes(-30), 20, 720);
        var yesterday = Stored(Now.AddDays(-1), 20, 900);
        var lastMonth = Stored(Now.AddDays(-20), 10, 300);

        var summary = Summarize(30, 0, today, yesterday, lastMonth);

        (summary.TodaySeconds, summary.Last7DaysSeconds, summary.TotalSeconds, summary.Sessions).ShouldBe((720.0, 1620.0, 1920.0, 3));
        summary.LastPractisedAt.ShouldBe(Now.AddMinutes(-10));
        summary.Days.Sum(day => day.Seconds).ShouldBe(1920);
    }

    [Fact]
    public void SessionsOutsideTheWindow_CountOnlyInTheTotal()
    {
        var summary = Summarize(1, 0, Stored(Now.AddDays(-3), 10, 300));

        summary.Days.ShouldBe([new PracticeDay(new DateOnly(2026, 11, 16), 0)]);
        (summary.TotalSeconds, summary.Last7DaysSeconds).ShouldBe((300.0, 300.0));
    }

    [Fact]
    public void SessionsAcrossLocalMidnight_AreSplitProportionally()
    {
        // 22:30-23:30 UTC = 23:30-00:30 at UTC+60: half before and half after local midnight.
        var session = Stored(new DateTime(2026, 11, 15, 22, 30, 0, DateTimeKind.Utc), 60, 3000);

        var summary = Summarize(3, 60, session);

        summary.Days.ShouldBe([
            new PracticeDay(new DateOnly(2026, 11, 14), 0),
            new PracticeDay(new DateOnly(2026, 11, 15), 1500),
            new PracticeDay(new DateOnly(2026, 11, 16), 1500),
        ]);
        summary.TodaySeconds.ShouldBe(1500);
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(840, 16)]
    [InlineData(-840, 15)]
    public void LocalDay_UsesTheOffset(int offset, int day) =>
        LocalDay.DateOf(new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc), offset).ShouldBe(new DateOnly(2026, 11, day));

    [Fact]
    public void ZeroLengthSessions_CountOnTheirStartDay() =>
        LocalDay.Split(Now, Now, 4, 0).ShouldBe([(new DateOnly(2026, 11, 16), 4.0)]);

    [Fact]
    public void Sections_GroupById_OrByLabelAndKind_WithTheBestTrainerPercent()
    {
        var unnamed = Part(30, null, "", PracticeSectionKind.None);
        var first = Stored(Now.AddDays(-1), 10, 600, Part(200, Solo, "Solo") with { TrainerReachedPercent = 80 }, unnamed, Part(10, null, "Bridge", PracticeSectionKind.Bridge));
        var second = Stored(Now.AddMinutes(-20), 10, 600, Part(300, Solo, "Solo (old)") with { TrainerReachedPercent = 90 }, Part(5, Solo, "Solo"), unnamed);

        var summary = PracticeSummaryCalculator.Summarize([first, second], new SummaryWindow(Now, 30, 0), new Dictionary<Guid, string> { [Solo] = "Guitar solo" });

        summary.Sections.Select(total => (total.SectionId, total.Label, total.Kind, total.Seconds, total.LoopPasses, total.BestTrainerPercent)).ShouldBe([
            ((Guid?)Solo, "Guitar solo", PracticeSectionKind.Solo, 505.0, 6, (double?)90),
            (null, "", PracticeSectionKind.None, 60.0, 4, null),
            (null, "Bridge", PracticeSectionKind.Bridge, 10.0, 2, null),
        ]);
        summary.Sections[0].LastPractisedAt.ShouldBe(Now.AddMinutes(-10));
    }

    [Fact]
    public void DeletedSections_KeepTheirLatestSnapshotLabel()
    {
        var older = Stored(Now.AddDays(-2), 10, 600, Part(100, Solo, "Old label"));
        var newer = Stored(Now.AddDays(-1), 10, 600, Part(100, Solo, "New label"));

        Summarize(30, 0, newer, older).Sections.Single().Label.ShouldBe("New label");
    }

    [Fact]
    public void Tempo_IsTheHighestTrainerPercentPerDayAndSection_InTheWindow()
    {
        var dayOne = Stored(Now.AddDays(-1), 10, 600, Part(100, Solo, "Solo") with { TrainerReachedPercent = 75 }, Part(100, Solo, "Solo") with { TrainerReachedPercent = 80 });
        var dayTwo = Stored(Now.AddMinutes(-20), 10, 600, Part(100, Solo, "Solo") with { TrainerReachedPercent = 90 }, Part(50, null, "Verse", PracticeSectionKind.Verse));
        var outside = Stored(Now.AddDays(-40), 10, 600, Part(100, Solo, "Solo") with { TrainerReachedPercent = 60 });

        var summary = Summarize(30, 0, dayTwo, outside, dayOne);

        summary.Tempo.ShouldBe([
            new PracticeTempoPoint(new DateOnly(2026, 11, 15), Solo, "Solo", 80),
            new PracticeTempoPoint(new DateOnly(2026, 11, 16), Solo, "Solo", 90),
        ]);
    }

    [Fact]
    public void Overview_TotalsPerTrack_MostRecentFirst()
    {
        var other = Guid.NewGuid();
        var old = Stored(Now.AddDays(-30), 10, 600);
        var recent = PracticeSession.Create(Guid.NewGuid(), Session(Now.AddMinutes(-15), 10, 300) with { TrackId = other }, Now);
        var oldAgain = Stored(Now.AddDays(-2), 10, 120);

        var overview = PracticeSummaryCalculator.Overview([old, recent, oldAgain], new SummaryWindow(Now, 7, 0));

        overview.Select(total => (total.TrackId, total.TotalSeconds, total.Last7DaysSeconds, total.Sessions)).ShouldBe([(other, 300.0, 300.0, 1), (TrackId, 720.0, 120.0, 2)]);
        overview[0].LastPractisedAt.ShouldBe(Now.AddMinutes(-5));
    }

    [Fact]
    public void Window_RefusesBadValues()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SummaryWindow(Now, 0, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new SummaryWindow(Now, 366, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new SummaryWindow(Now, 30, 841));
        Should.Throw<ArgumentOutOfRangeException>(() => new SummaryWindow(Now, 30, -841));
    }
}
