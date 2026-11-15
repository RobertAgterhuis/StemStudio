using System.Text.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Voices;
using static StemStudio.Api.Tests.Contract.ContractJson;
using static StemStudio.Api.Tests.Contract.GenerationContractTests;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, features 009 + 010 Phase 0, 010 T004): lyrics sheet, song, voice profile and voice conversion DTOs,
/// routes and limits against backend-api 1.9.0, and the 010 generator example copies.
/// </summary>
public sealed class SongVoiceContractTests
{
    private static readonly (Type Enum, string Schema)[] EnumSchemas =
    [
        (typeof(LeadVoiceDto), "LeadVoice"), (typeof(VocalLabelDto), "VocalLabel"), (typeof(SectionMarkerDto), "SectionMarker"),
        (typeof(VoiceSampleSourceDto), "VoiceSampleSource"),
    ];

    [Fact]
    public void SongVoiceEnums_MatchTheContract() => AssertEnumsMatch(EnumSchemas);

    [Fact]
    public void LyricsLanguages_MatchTheContractAndTheGenerator()
    {
        ContractLimits.LyricsLanguages.ShouldBe(Strings(Schema("LyricsLanguage")["enum"]!));
        ContractLimits.LyricsLanguages.ShouldBe(Strings(GeneratorContract.Root["components"]!["schemas"]!["VocalLanguage"]!["enum"]!));
    }

    [Fact]
    public void LyricsExamples_RoundTrip()
    {
        AssertRoundTrip<LyricsInput>("LyricsInputExample", "LyricsInput");
        AssertRoundTrip<LyricsSheetDto>("LyricsSheetExample", "LyricsSheet");
        AssertRoundTrip<SectionProposalDto>("SectionProposalExample", "SectionProposal");
    }

    [Theory]
    [InlineData("SongRequestExample")]
    [InlineData("InstrumentalSongRequestExample")]
    public void SongRequests_RoundTrip(string example) => AssertRoundTrip<SongRequest>(example, "SongRequest");

    [Theory]
    [InlineData("QueuedSongGeneration")]
    [InlineData("CompletedSongGeneration")]
    [InlineData("WaitingForStemsConversion")]
    [InlineData("CompletedVoiceConversionGeneration")]
    public void SongAndConversionGenerations_RoundTrip(string example) => AssertRoundTrip<GenerationDto>(example, "Generation");

    [Fact]
    public void VoiceExamples_RoundTrip()
    {
        AssertRoundTrip<VoiceProfileDto>("VoiceProfileExample", "VoiceProfile");
        AssertRoundTrip<ConsentStatementDto>("ConsentStatementV1", "ConsentStatement");
    }

    [Fact]
    public void ConversionTake_IsLabelledWithTheProfileAndServesItsVocal()
    {
        var dto = Example("CompletedVoiceConversionGeneration").Deserialize<GenerationDto>(Web)!;
        var take = dto.Takes.Single();

        (take.Vocal, take.VoiceProfileName, take.VocalAudioUrl).ShouldBe((VocalLabelDto.Converted, "My voice", ApiRoutes.TakeVocal(dto.Id, 1)));
        (dto.Restyle, dto.Song, dto.VoiceConversion!.VoiceProfileName).ShouldBe(((RestyleDetailsDto?)null, (SongDetailsDto?)null, "My voice"));
    }

    [Fact]
    public void ConsentStatement_VersionAndFormFieldsMatchTheContract()
    {
        var statement = Example("ConsentStatementV1").Deserialize<ConsentStatementDto>(Web)!;
        var form = Schema("CreateVoiceProfileRequest")["required"]!;

        statement.Version.ShouldBe(ContractLimits.ConsentStatementVersion);
        Schema("VoiceConsent")["properties"]!["statementVersion"]!["const"]!.GetValue<string>().ShouldBe(ContractLimits.ConsentStatementVersion);
        Strings(form).ShouldBe([VoiceProfileForm.Sample, VoiceProfileForm.DisplayName, VoiceProfileForm.PersonName, VoiceProfileForm.ConsentConfirmed,
            VoiceProfileForm.ConsentStatementVersion, VoiceProfileForm.Source]);
        Schema("LyricsUploadRequest")["properties"]!.AsObject().Select(p => p.Key).ShouldBe([LyricsUploadForm.File, LyricsUploadForm.Title, LyricsUploadForm.Language]);
    }

    [Fact]
    public void SongAndVoiceLimits_MatchTheContract()
    {
        var song = Schema("SongRequest")["properties"]!;

        Schema("LyricsSheet")["properties"]!["text"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.LyricsSheetMaxLength);
        Schema("LyricsSheet")["properties"]!["title"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.LyricsTitleMaxLength);
        (song["bpm"]!["minimum"]!.GetValue<double>(), song["bpm"]!["maximum"]!.GetValue<double>()).ShouldBe((ContractLimits.MinSongBpm, ContractLimits.MaxSongBpm));
        song["durationSeconds"]!["maximum"]!.GetValue<double>().ShouldBe(ContractLimits.MaxGenerationSeconds);
        song["freeCaption"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.FreeCaptionMaxLength);
        song["delivery"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.DeliveryMaxLength);
        song["keyScale"]!["pattern"]!.GetValue<string>().ShouldBe(ContractLimits.SongKeyPattern);
        Schema("VoiceProfile")["properties"]!["durationSeconds"]!["maximum"]!.GetValue<double>().ShouldBe(ContractLimits.MaxVoiceSampleSeconds);
        Schema("VoiceConversionRequest")["properties"]!["pitchShift"]!["maximum"]!.GetValue<double>().ShouldBe(ContractLimits.MaxPitchShift);
    }

    [Fact]
    public void Routes_ExistInTheContract()
    {
        var id = Guid.NewGuid();
        var routes = new[]
        {
            (ApiRoutes.LyricsSheets, "/api/lyrics"), (ApiRoutes.LyricsSheetUpload, "/api/lyrics/upload"), (ApiRoutes.LyricsSections, "/api/lyrics/sections"),
            (ApiRoutes.LyricsSheet(id), "/api/lyrics/{lyricsId}"), (ApiRoutes.Songs, "/api/songs"), (ApiRoutes.VoiceProfiles, "/api/voice-profiles"),
            (ApiRoutes.VoiceProfile(id), "/api/voice-profiles/{profileId}"), (ApiRoutes.VoiceSample(id), "/api/voice-profiles/{profileId}/sample"),
            (ApiRoutes.VoiceConsent, "/api/consent/voice"), (ApiRoutes.VoiceConversions(id, 1), "/api/generations/{generationId}/takes/{index}/voice-conversions"),
            (ApiRoutes.TakeVocal(id, 1), "/api/generations/{generationId}/takes/{index}/vocal"),
        };

        AssertRoutesExist(routes, id);
    }


    [Theory]
    [InlineData("SongRequest")]
    [InlineData("InstrumentalSongRequest")]
    [InlineData("VoiceConversionParams")]
    [InlineData("QueuedSong")]
    [InlineData("RunningSong")]
    [InlineData("CompletedSong")]
    [InlineData("CompletedInstrumental")]
    [InlineData("CompletedVoiceConversion")]
    [InlineData("FailedUnvoicedTarget")]
    [InlineData("VoiceSampleOk")]
    [InlineData("VoiceSampleUnvoiced")]
    public void GeneratorExampleCopies_Are010ExamplesOfTheContract(string file) => IsGeneratorCopyOf(file, GeneratorJson(file)).ShouldBeTrue(file);
}
