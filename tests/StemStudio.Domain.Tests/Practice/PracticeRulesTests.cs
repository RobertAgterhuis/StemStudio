using StemStudio.Domain.Practice;

namespace StemStudio.Domain.Tests.Practice;

/// <summary>Practice sessions, the log settings and the session rules (014 FR-025, FR-028, data-model 3.7).</summary>
public sealed class PracticeRulesTests
{
    internal static readonly DateTime Now = new(2026, 11, 16, 9, 42, 0, DateTimeKind.Utc);
    internal static readonly Guid TrackId = Guid.NewGuid();

    internal static PracticeSessionInput Session(DateTime start, double minutes, double played, params PracticeSectionInput[] sections) => new()
    {
        TrackId = TrackId,
        StartedAt = start,
        EndedAt = start.AddMinutes(minutes),
        PlayedSeconds = played,
        LoopPasses = 3,
        MaxSpeedPercent = 90,
        Sections = sections,
    };

    internal static PracticeSectionInput Part(double played, Guid? id = null, string label = "Guitar solo", PracticeSectionKind kind = PracticeSectionKind.Solo) =>
        new() { SectionId = id, Kind = kind, Label = label, PlayedSeconds = played, LoopPasses = 2 };

    private static string[] Fields(PracticeSessionInput input) => PracticeSessionRules.Validate(input, Now).Select(error => error.Field).ToArray();

    [Fact]
    public void ValidSession_HasNoErrors() =>
        Fields(Session(Now.AddMinutes(-10), 10, 600, Part(300, Guid.NewGuid()) with { TrainerReachedPercent = 80 }, Part(301, kind: PracticeSectionKind.None, label: ""))).ShouldBeEmpty();

    [Fact]
    public void EndBeforeStart_IsRefused() => Fields(Session(Now, -1, 0)).ShouldBe(["endedAt"]);

    [Fact]
    public void MoreThan12Hours_IsRefused()
    {
        Fields(Session(Now.AddHours(-13), (12 * 60) + 1, 0)).ShouldBe(["endedAt"]);
        Fields(Session(Now.AddHours(-13), 12 * 60, 0)).ShouldBeEmpty();
    }

    [Fact]
    public void StartMoreThan5MinutesInTheFuture_IsRefused()
    {
        Fields(Session(Now.AddMinutes(5.1), 1, 0)).ShouldBe(["startedAt"]);
        Fields(Session(Now.AddMinutes(5), 1, 0)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(66)]
    public void PlayedOutsideTheSpanPlus5_IsRefused(double played) => Fields(Session(Now.AddMinutes(-2), 1, played)).ShouldBe(["playedSeconds"]);

    [Fact]
    public void PlayedUpToTheSpanPlus5_IsAllowed() => Fields(Session(Now.AddMinutes(-2), 1, 65)).ShouldBeEmpty();

    [Fact]
    public void PlayedAbove12Hours_IsRefused() => Fields(Session(Now.AddHours(-12), 12 * 60, 43_201)).ShouldContain("playedSeconds");

    [Theory]
    [InlineData(49.9)]
    [InlineData(120.1)]
    public void MaxSpeedOutside50To120_IsRefused(double percent) =>
        Fields(Session(Now.AddMinutes(-2), 1, 30) with { MaxSpeedPercent = percent }).ShouldBe(["maxSpeedPercent"]);

    [Fact]
    public void NegativeLoopPasses_AreRefused() => Fields(Session(Now.AddMinutes(-2), 1, 30) with { LoopPasses = -1 }).ShouldBe(["loopPasses"]);

    [Fact]
    public void SectionsSumAboveThePlayedTimePlus1_IsRefused()
    {
        Fields(Session(Now.AddMinutes(-2), 1, 30, Part(20), Part(11.5))).ShouldBe(["sections"]);
        Fields(Session(Now.AddMinutes(-2), 1, 30, Part(20), Part(11))).ShouldBeEmpty();
    }

    [Fact]
    public void SectionRowRules_AreChecked()
    {
        var bad = Session(Now.AddMinutes(-2), 1, 60, Part(-1), Part(1, label: new string('l', 41)), Part(1) with { TrainerReachedPercent = 121 }, Part(1) with { LoopPasses = -1 });

        Fields(bad).ShouldBe(["sections[0].playedSeconds", "sections[1].label", "sections[2].trainerReachedPercent", "sections[3].loopPasses"]);
    }

    [Fact]
    public void MoreThan64SectionRows_AreRefused()
    {
        var rows = Enumerable.Range(0, 65).Select(_ => Part(0)).ToArray();

        Fields(Session(Now.AddMinutes(-2), 1, 60, rows)).ShouldBe(["sections"]);
    }

    [Fact]
    public void Create_StoresTheSessionAndItsSectionRows()
    {
        var id = Guid.NewGuid();
        var section = Guid.NewGuid();

        var session = PracticeSession.Create(id, Session(Now.AddMinutes(-10), 10, 500, Part(200, section, " Solo ") with { TrainerReachedPercent = 85 }), Now);

        (session.Id, session.TrackId, session.PlayedSeconds, session.LoopPasses, session.MaxSpeedPercent).ShouldBe((id, TrackId, 500.0, 3, (double?)90));
        session.Sections.Single().ShouldSatisfyAllConditions(
            row => row.SessionId.ShouldBe(id),
            row => row.Index.ShouldBe(0),
            row => row.SectionId.ShouldBe(section),
            row => row.Label.ShouldBe("Solo"),
            row => row.TrainerReachedPercent.ShouldBe(85),
            row => row.LoopPasses.ShouldBe(2),
            row => row.Kind.ShouldBe(PracticeSectionKind.Solo));
    }

    [Fact]
    public void Replace_TakesTheCumulativeValues_AndReplacesTheRows()
    {
        var session = PracticeSession.Create(Guid.NewGuid(), Session(Now.AddMinutes(-10), 5, 200, Part(100), Part(50)), Now);

        session.Replace(Session(Now.AddMinutes(-10), 10, 550, Part(300)) with { MixId = Guid.Empty, LoopPasses = 7 }, Now);

        (session.PlayedSeconds, session.LoopPasses, session.EndedAt, session.MixId).ShouldBe((550.0, 7, Now, (Guid?)Guid.Empty));
        session.Sections.Count.ShouldBe(1);
    }

    [Fact]
    public void Replace_ForAnotherTrack_IsRefused()
    {
        var session = PracticeSession.Create(Guid.NewGuid(), Session(Now.AddMinutes(-10), 5, 200), Now);

        Should.Throw<DomainValidationException>(() => session.Replace(Session(Now.AddMinutes(-10), 5, 200) with { TrackId = Guid.NewGuid() }, Now))
            .Errors.Single().Field.ShouldBe("trackId");
    }

    [Fact]
    public void Create_WithInvalidInput_Throws() =>
        Should.Throw<DomainValidationException>(() => PracticeSession.Create(Guid.NewGuid(), Session(Now, -1, 0), Now));

    [Fact]
    public void LogSettings_DefaultOn_AndUpdateChangesOnlyGivenFields()
    {
        var settings = PracticeLogSettings.CreateDefault();
        (settings.Id, settings.Enabled, settings.NoticeAcknowledged, settings.UpdatedAt).ShouldBe((PracticeLogSettings.SingletonId, true, false, (DateTime?)null));

        settings.Update(enabled: false, noticeAcknowledged: null, Now);
        (settings.Enabled, settings.NoticeAcknowledged, settings.UpdatedAt).ShouldBe((false, false, (DateTime?)Now));
        settings.Update(enabled: null, noticeAcknowledged: true, Now.AddMinutes(1));
        (settings.Enabled, settings.NoticeAcknowledged).ShouldBe((false, true));
    }
}
