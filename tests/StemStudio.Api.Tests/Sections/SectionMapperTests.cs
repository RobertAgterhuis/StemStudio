using StemStudio.Api.SongSections;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Sections;
using StemStudio.Domain.Sections;

namespace StemStudio.Api.Tests.SongSections;

/// <summary>The section mapper casts domain enums to contract enums: their members must line up one to one.</summary>
public sealed class SectionMapperTests
{
    public static TheoryData<Type, Type> EnumPairs => new()
    {
        { typeof(SectionKind), typeof(SectionKindDto) },
        { typeof(SectionOrigin), typeof(SectionOriginDto) },
        { typeof(SectionSetOrigin), typeof(SectionSetOriginDto) },
        { typeof(SectionSetStatus), typeof(SectionSetStatusDto) },
        { typeof(SectionSnap), typeof(SectionSnapDto) },
    };

    [Theory]
    [MemberData(nameof(EnumPairs))]
    public void DomainAndContractEnums_LineUp(Type domain, Type contract) =>
        Enum.GetNames(domain).ShouldBe(Enum.GetNames(contract));

    [Theory]
    [InlineData("pre-chorus", SectionKind.PreChorus)]
    [InlineData("instrumental", SectionKind.Instrumental)]
    [InlineData("drop", SectionKind.Other)]
    [InlineData(null, SectionKind.Other)]
    [InlineData("\"", SectionKind.Other)]
    public void ParseKind_ReadsKebabCase_AndFallsBackToOther(string? wire, SectionKind kind) => SectionMapper.ParseKind(wire).ShouldBe(kind);

    [Fact]
    public void KindName_IsKebabCase() => SectionMapper.KindName(SectionKind.PreChorus).ShouldBe("pre-chorus");

    [Fact]
    public void Detection_DropsUnknownWarningsAndSnaps_OnTheWayOut()
    {
        var result = new WorkerSectionResult(new WorkerAnalyzer("fake", "1.0.0"), "wobble", [new WorkerSectionWarning("no-vocals", "No vocals."), new WorkerSectionWarning("odd", "?")],
            [new WorkerDetectedSection("drop", "Drop", 0, 10, "A", null, 0.5)]);

        var detection = SectionMapper.ToDetection(result);
        var set = TrackSections.Create(Guid.NewGuid(), DateTime.UnixEpoch);
        set.ApplyDetected(detection, Guid.NewGuid, DateTime.UnixEpoch);
        var dto = SectionMapper.ToDto(TestSupport.SyncTestData.CompletedTrack(), set);

        (detection.Analyzer, detection.Snap).ShouldBe(("fake/1.0.0", (SectionSnap?)null));
        dto.Warnings.Select(warning => warning.Code).ShouldBe([SectionWarningCodeDto.NoVocals]);
        dto.Sections.Single().Kind.ShouldBe(SectionKindDto.Other);
    }
}
