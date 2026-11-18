using StemStudio.Domain.Sections;
using StemStudio.Domain.Tests.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Tests.Sections;

/// <summary>Section rules and the section set aggregate (014 FR-002, FR-006, data-model 3.1-3.2).</summary>
public sealed class SectionRulesTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
    private static readonly SectionContext Context = new(64, new HashSet<string>(StringComparer.Ordinal) { "vocals", "guitar", "guitar.electric-guitar" });
    private static readonly Guid TrackId = Guid.NewGuid();

    public static SectionInput Input(double start, double end, SectionKind kind = SectionKind.Verse, string label = "Verse") =>
        new(null, kind, label, start, end);

    private static IReadOnlyList<DomainError> Validate(params SectionInput[] sections) => SectionRules.Validate(sections, Context, new HashSet<Guid>());

    private static string[] Fields(IReadOnlyList<DomainError> errors) => errors.Select(error => error.Field).ToArray();

    public static DetectionResult Detected(params DetectedSection[] sections) =>
        new("stems-novelty/1.0.0", SectionSnap.Downbeat, [new SectionWarning("no-vocals", "No vocals.")], sections);

    public static DetectedSection Found(double start, double end, SectionKind kind = SectionKind.Verse, string label = "Verse") =>
        new(kind, label, start, end, null, 0.8);

    [Fact]
    public void ValidList_HasNoErrors()
    {
        Validate(Input(0, 8, SectionKind.Intro, "Intro"), Input(8, 40), Input(41, 64, SectionKind.Solo, "Solo") with { Instrument = "guitar.electric-guitar" }).ShouldBeEmpty();
    }

    [Fact]
    public void EmptyList_IsRefused() => Fields(Validate()).ShouldBe(["sections"]);

    [Fact]
    public void MoreThan64Sections_AreRefused()
    {
        var context = Context with { DurationSeconds = 900 };
        var many = Enumerable.Range(0, 65).Select(i => Input(i * 2, (i * 2) + 1.5)).ToArray();

        Fields(SectionRules.Validate(many, context, new HashSet<Guid>())).ShouldBe(["sections"]);
        SectionRules.Validate(many[..64], context, new HashSet<Guid>()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-0.1, 5, "sections[0].startSeconds")]
    [InlineData(0, 64.5, "sections[0].endSeconds")]
    [InlineData(5, 5.99, "sections[0].endSeconds")]
    [InlineData(6, 5, "sections[0].endSeconds")]
    public void Bounds_AreChecked(double start, double end, string field) => Fields(Validate(Input(start, end))).ShouldContain(field);

    [Fact]
    public void ExactlyOneSecond_IsAllowed() => Validate(Input(5, 6)).ShouldBeEmpty();

    [Fact]
    public void Overlap_AndUnsortedOrder_AreRefused()
    {
        Fields(Validate(Input(0, 10), Input(9, 20))).ShouldBe(["sections[1].startSeconds"]);
        Fields(Validate(Input(20, 30), Input(0, 10))).ShouldBe(["sections[1].startSeconds"]);
    }

    [Fact]
    public void Gaps_AreAllowed() => Validate(Input(0, 10), Input(20, 30)).ShouldBeEmpty();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678901234567890123456789012345678901")]
    public void BadLabels_AreRefused(string label) => Fields(Validate(Input(0, 10, label: label))).ShouldBe(["sections[0].label"]);

    [Fact]
    public void LabelOf40CharactersAfterTrim_IsAllowed() => Validate(Input(0, 10, label: "  " + new string('a', 40) + " ")).ShouldBeEmpty();

    [Fact]
    public void Instrument_OnlyForSolo_AndOnlyAChannelOfTheTrack()
    {
        Fields(Validate(Input(0, 10, SectionKind.Verse) with { Instrument = "guitar" })).ShouldBe(["sections[0].instrument"]);
        Fields(Validate(Input(0, 10, SectionKind.Solo) with { Instrument = "piano" })).ShouldBe(["sections[0].instrument"]);
        Validate(Input(0, 10, SectionKind.Solo) with { Instrument = "guitar" }).ShouldBeEmpty();
        Validate(Input(0, 10, SectionKind.Solo)).ShouldBeEmpty();
    }

    [Fact]
    public void Ids_MustBelongToTheTrack_AndBeUnique()
    {
        var own = Guid.NewGuid();
        var owned = new HashSet<Guid> { own };

        Fields(SectionRules.Validate([Input(0, 10) with { Id = Guid.NewGuid() }], Context, owned)).ShouldBe(["sections[0].id"]);
        Fields(SectionRules.Validate([Input(0, 10) with { Id = own }, Input(10, 20) with { Id = own }], Context, owned)).ShouldBe(["sections[1].id"]);
        SectionRules.Validate([Input(0, 10) with { Id = own }, Input(10, 20)], Context, owned).ShouldBeEmpty();
    }

    [Fact]
    public void ChannelsOf_ATrack_AreItsStemsAndSplitDetailStems()
    {
        var channels = SectionChannels.Of(DetailTestData.DetailCompleted());

        channels.ShouldContain("guitar");
        channels.ShouldContain("drums.snare");
        channels.ShouldContain("vocals.lead");
        channels.ShouldNotContain("guitar.electric-guitar");
        SectionChannels.Of(DetailTestData.CompletedTrack("htdemucs")).ShouldNotContain("guitar");
    }

    [Fact]
    public void NewSet_HasStatusNone_AndNoSections()
    {
        var set = TrackSections.Create(TrackId, T0);

        (set.TrackId, set.Status, set.Origin, set.Revision, set.Sections.Count).ShouldBe((TrackId, SectionSetStatus.None, SectionSetOrigin.Detected, 0, 0));
        set.Warnings.ShouldBeEmpty();
        set.UpdatedAt.ShouldBe(T0);
    }

    [Fact]
    public void StartDetection_QueuesWithTheJob_AndApplyDetected_StoresNewIds()
    {
        var set = TrackSections.Create(TrackId, T0);
        set.StartDetection("0123456789abcdef0123456789abcdef", T0.AddSeconds(1));
        (set.Status, set.WorkerJobId, set.IsDetecting).ShouldBe((SectionSetStatus.Queued, "0123456789abcdef0123456789abcdef", true));
        set.MarkDetecting(T0.AddSeconds(2));
        set.Status.ShouldBe(SectionSetStatus.Detecting);

        set.ApplyDetected(Detected(Found(0, 8, SectionKind.Intro, "Intro"), Found(8, 64)), Guid.NewGuid, T0.AddSeconds(3));

        (set.Status, set.Revision, set.WorkerJobId, set.Analyzer, set.Snap, set.UpdatedAt).ShouldBe((SectionSetStatus.Ready, 1, (string?)null, "stems-novelty/1.0.0", (SectionSnap?)SectionSnap.Downbeat, T0.AddSeconds(3)));
        set.Warnings.ShouldBe([new SectionWarning("no-vocals", "No vocals.")]);
        set.Sections.Select(section => (section.Position, section.Kind, section.Origin, section.TrackId)).ShouldBe(
            [(0, SectionKind.Intro, SectionOrigin.Detected, TrackId), (1, SectionKind.Verse, SectionOrigin.Detected, TrackId)]);
        set.Sections[0].Confidence.ShouldBe(0.8);
    }

    [Fact]
    public void MarkDetecting_OnlyChangesAQueuedSet()
    {
        var set = TrackSections.Create(TrackId, T0);

        set.MarkDetecting(T0.AddSeconds(1));

        set.Status.ShouldBe(SectionSetStatus.None);
    }

    [Fact]
    public void ApplyDetected_ReplacesEditedSections_WithNewIds_AndTrimsLabelsAndTheCount()
    {
        var set = ReadySet();
        var old = set.Sections[0].Id;
        var many = Enumerable.Range(0, 70).Select(i => Found(i, i + 1, label: "  " + new string('x', 50))).ToArray();

        set.ApplyDetected(Detected(many), Guid.NewGuid, T0.AddMinutes(5));

        set.Sections.Count.ShouldBe(64);
        set.Sections.ShouldAllBe(section => section.Id != old && section.Label.Length == 40);
        (set.Origin, set.Revision).ShouldBe((SectionSetOrigin.Detected, 2));
    }

    [Fact]
    public void ApplyDetected_UnknownLabel_FallsBackToTheKindName()
    {
        var set = TrackSections.Create(TrackId, T0);

        set.ApplyDetected(Detected(Found(0, 8, SectionKind.PreChorus, " ")), Guid.NewGuid, T0);

        set.Sections[0].Label.ShouldBe("Pre-chorus");
    }

    [Fact]
    public void Fail_StoresTheReason_AndClearsTheJob()
    {
        var set = TrackSections.Create(TrackId, T0);
        set.StartDetection("0123456789abcdef0123456789abcdef", T0);

        set.Fail(new string('e', 600), T0.AddSeconds(4));

        (set.Status, set.WorkerJobId, set.Error!.Length).ShouldBe((SectionSetStatus.Failed, (string?)null, 500));
        set.Fail(null, T0.AddSeconds(5));
        set.Error.ShouldBe(TrackSections.DefaultFailure);
    }

    [Fact]
    public void ResetToNone_AfterAFailedSubmission()
    {
        var set = TrackSections.Create(TrackId, T0);
        set.StartDetection("0123456789abcdef0123456789abcdef", T0);

        set.ResetToNone(T0.AddSeconds(1));

        (set.Status, set.WorkerJobId).ShouldBe((SectionSetStatus.None, (string?)null));
    }

    [Theory]
    [InlineData("not-a-job")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    public void StartDetection_RefusesBadJobIds(string jobId) =>
        Should.Throw<ArgumentException>(() => TrackSections.Create(TrackId, T0).StartDetection(jobId, T0));

    [Fact]
    public void DetectRefusal_FollowsStatusAndOrigin()
    {
        var set = ReadySet();
        set.CheckDetect(overwrite: false).ShouldBe(DetectRefusal.None);
        set.Replace(new SectionSetUpdate(1, [Input(0, 64)]), Context, Guid.NewGuid, T0.AddMinutes(1));
        set.CheckDetect(overwrite: false).ShouldBe(DetectRefusal.SectionsEdited);
        set.CheckDetect(overwrite: true).ShouldBe(DetectRefusal.None);
        set.StartDetection("0123456789abcdef0123456789abcdef", T0.AddMinutes(2));
        set.CheckDetect(overwrite: true).ShouldBe(DetectRefusal.DetectionRunning);
    }

    [Fact]
    public void Replace_KeepsIds_AndOnlyChangedSectionsBecomeUserSections()
    {
        var set = ReadySet();
        var (intro, verse) = (set.Sections[0], set.Sections[1]);

        set.Replace(
            new SectionSetUpdate(1, [Input(0, 8, SectionKind.Intro, "Intro") with { Id = intro.Id }, Input(8, 30) with { Id = verse.Id }, Input(30, 64, SectionKind.Chorus, "Chorus")]),
            Context,
            Guid.NewGuid,
            T0.AddMinutes(1));

        (set.Origin, set.Revision, set.Status).ShouldBe((SectionSetOrigin.Edited, 2, SectionSetStatus.Ready));
        set.Sections.Select(section => section.Origin).ShouldBe([SectionOrigin.Detected, SectionOrigin.User, SectionOrigin.User]);
        set.Sections.Select(section => section.Position).ShouldBe([0, 1, 2]);
        set.Sections[0].Id.ShouldBe(intro.Id);
        set.Sections[0].Confidence.ShouldBe(0.8);
        set.Sections[1].Id.ShouldBe(verse.Id);
        set.Sections[1].Confidence.ShouldBeNull();
        set.Sections[2].Id.ShouldNotBe(intro.Id);
    }

    /// <summary>The detected intro (0-8 s, "Intro") with one field changed, named by the change.</summary>
    public static TheoryData<string, SectionInput> IntroChanges => new()
    {
        { "kind", Input(0, 8, SectionKind.Bridge, "Intro") },
        { "label", Input(0, 8, SectionKind.Intro, "Start") },
        { "end", Input(0, 9, SectionKind.Intro, "Intro") },
        { "start", Input(1, 8, SectionKind.Intro, "Intro") },
        { "instrument", Input(0, 8, SectionKind.Solo, "Intro") with { Instrument = "guitar" } },
    };

    [Theory]
    [MemberData(nameof(IntroChanges))]
    public void AnyChangeOfKindLabelBoundsOrInstrument_MakesAUserSection(string change, SectionInput changed)
    {
        var set = ReadySet();
        var intro = set.Sections[0];

        set.Replace(new SectionSetUpdate(1, [changed with { Id = intro.Id }, Input(9, 64) with { Id = set.Sections[1].Id }]), Context, Guid.NewGuid, T0);

        set.Sections[0].Origin.ShouldBe(SectionOrigin.User, change);
    }

    [Fact]
    public void Replace_TrimsLabels_AndDropsSectionsThatAreNotSent()
    {
        var set = ReadySet();

        set.Replace(new SectionSetUpdate(1, [Input(0, 64, label: "  Whole song  ")]), Context, Guid.NewGuid, T0);

        set.Sections.Count.ShouldBe(1);
        set.Sections[0].Label.ShouldBe("Whole song");
    }

    [Fact]
    public void Replace_WithAStaleRevision_Throws()
    {
        var set = ReadySet();

        Should.Throw<SectionsChangedException>(() => set.Replace(new SectionSetUpdate(0, [Input(0, 64)]), Context, Guid.NewGuid, T0));
        set.Revision.ShouldBe(1);
    }

    [Fact]
    public void Replace_WithInvalidSections_ThrowsFieldErrors_AndChangesNothing()
    {
        var set = ReadySet();

        var error = Should.Throw<DomainValidationException>(() => set.Replace(new SectionSetUpdate(1, [Input(0, 0.5)]), Context, Guid.NewGuid, T0));

        error.Errors.Select(e => e.Field).ShouldBe(["sections[0].endSeconds"]);
        (set.Revision, set.Sections.Count).ShouldBe((1, 2));
    }

    [Fact]
    public void Replace_WhileDetecting_CancelsTheDetection()
    {
        var set = ReadySet();
        set.StartDetection("0123456789abcdef0123456789abcdef", T0);

        set.Replace(new SectionSetUpdate(1, [Input(0, 64)]), Context, Guid.NewGuid, T0);

        (set.Status, set.WorkerJobId).ShouldBe((SectionSetStatus.Ready, (string?)null));
    }

    [Fact]
    public void SectionExceptionConstructors_CarryMessages()
    {
        new SectionsChangedException().Message.ShouldNotBeEmpty();
        new SectionsChangedException("x").Message.ShouldBe("x");
        new SectionsChangedException("y", new InvalidOperationException()).InnerException.ShouldNotBeNull();
    }

    private static TrackSections ReadySet()
    {
        var set = TrackSections.Create(TrackId, T0);
        set.ApplyDetected(Detected(Found(0, 8, SectionKind.Intro, "Intro"), Found(8, 64)), Guid.NewGuid, T0);
        return set;
    }

    internal static Track SixStemTrack() => DetailTestData.CompletedTrack();
}
