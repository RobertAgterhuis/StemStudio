using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;

namespace StemStudio.Agent.Tests.Ports;

/// <summary>Skeleton tests (architect, 011 Phase 0 part 2): the 009/010 port records and their invariants.</summary>
public sealed class PortRecordTests
{
    private static readonly Guid Id = Guid.Parse("4b6d8f0a-1c3e-4a5b-9d7f-2e4c6a8b0d1f");

    [Theory]
    [InlineData(ContractLimits.ConsentStatementVersion, true)]
    [InlineData("consent.v0", false)]
    public void VoiceProfileSummary_ConsentIsCurrentOnlyForTheCurrentStatement(string version, bool current)
    {
        var profile = new VoiceProfileSummary { ProfileId = Id, DisplayName = "My voice", ConsentStatementVersion = version, DurationSeconds = 24.5 };

        profile.ConsentCurrent.ShouldBe(current);
    }

    [Fact]
    public void LyricsSheetSummary_HasNoTextMember()
    {
        typeof(LyricsSheetSummary).GetProperties().Select(p => p.Name).ShouldBe(["LyricsId", "Title", "Language", "UpdatedAt"], ignoreOrder: true);
    }

    [Fact]
    public void VoiceProfileSummary_HasNoPersonalDataOrAudio()
    {
        var names = typeof(VoiceProfileSummary).GetProperties().Select(p => p.Name).ToList();

        names.ShouldNotContain("PersonName");
        names.ShouldNotContain("SampleUrl");
        names.ShouldNotContain("Consent");
    }

    [Fact]
    public void Queries_DefaultToNoFilter()
    {
        (new StyleQuery().Query, new StyleQuery().Kind, new StyleQuery().GenreFamily).ShouldBe((null, null, null));
        (new GenerationQuery().Kind, new GenerationQuery().SourceTrackId).ShouldBe((null, null));
        new StyleQuery { Kind = StyleKindDto.Preset, GenreFamily = GenreFamilyDto.World, Query = "reggae" }.ShouldNotBe(new StyleQuery());
        new GenerationQuery { Kind = GenerationKindDto.Song, SourceTrackId = Id }.SourceTrackId.ShouldBe(Id);
    }

    [Fact]
    public void StemStudioActions_HasOneGroupPerFeatureArea()
    {
        typeof(IStemStudioActions).GetProperties().Select(p => p.Name)
            .ShouldBe(["Tracks", "Mixes", "Artists", "Lyrics", "Assets", "Styles", "Generations", "Songs", "Voices"]);
    }

    [Fact]
    public void VoiceActions_CannotCreateOrDeleteProfiles()
    {
        typeof(IVoiceActions).GetMethods().Select(m => m.Name)
            .ShouldBe(["ListVoiceProfilesAsync", "CreateVoiceConversionAsync"], ignoreOrder: true);
    }
}
