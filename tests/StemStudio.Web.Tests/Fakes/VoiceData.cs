using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped consent statement, voice profiles and conversions (backend-api 1.9.0, feature 010).</summary>
public static class VoiceData
{
    public static readonly Guid ProfileId = Guid.Parse("6b1a3f2e-0d9c-4b8a-9f7e-d6c5b4a3f2e1");
    public static readonly Guid ConversionId = Guid.Parse("7c2b4a3f-1e0d-4c9b-8a8f-e7d6c5b4a3f2");

    public const string StatementEn = "This is my own voice. I agree that StemStudio uses it on this computer to convert generated vocals into my voice.";
    public const string StatementNl = "Dit is mijn eigen stem. Ik ga ermee akkoord dat StemStudio haar op deze computer gebruikt om gegenereerde zang naar mijn stem om te zetten.";

    public static ConsentStatementDto Statement() => new() { Version = "consent.v1", Text = StatementEn, TextNl = StatementNl };

    public static VoiceProfileDto Profile(string name = "My voice", int converted = 1) => new()
    {
        Id = ProfileId,
        DisplayName = name,
        Consent = new VoiceConsentDto { StatementVersion = "consent.v1", PersonName = "Sam Example", ConsentedAt = TestData.Created },
        Source = VoiceSampleSourceDto.Recorded,
        DurationSeconds = 21.4,
        VoicedRatio = 0.84,
        SampleUrl = ApiRoutes.VoiceSample(ProfileId),
        CreatedAt = TestData.Created,
        ConvertedTakeCount = converted,
    };

    public static GenerationDto Conversion() => GenerationData.Generation(GenerationStateDto.Completed, ConversionId) with
    {
        Kind = GenerationKindDto.VoiceConversion,
        Restyle = null,
        VoiceConversion = new VoiceConversionDetailsDto
        {
            SourceGenerationId = SongData.SongId,
            SourceTakeIndex = 1,
            VoiceProfileId = ProfileId,
            VoiceProfileName = "My voice",
            PitchShift = -2,
            AutoShift = false,
        },
        Takes = [GenerationData.Take(1, ConversionId) with
        {
            Vocal = VocalLabelDto.Converted,
            VoiceProfileName = "My voice",
            VocalAudioUrl = ApiRoutes.TakeVocal(ConversionId, 1),
        }],
    };

    public static GenerationStatusDto VoiceStatus(GenerationAvailabilityDto voice, string? reason = null) =>
        GenerationData.Status() with { Voice = new VoiceServiceStatusDto { State = voice, Reason = reason } };
}
