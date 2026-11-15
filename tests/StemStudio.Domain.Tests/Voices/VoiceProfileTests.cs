using System.Security.Cryptography;
using System.Text;
using StemStudio.Domain.Voices;

namespace StemStudio.Domain.Tests.Voices;

/// <summary>010 T016: voice profiles and consent (FR-005, FR-006, FR-007).</summary>
public sealed class VoiceProfileTests
{
    private static readonly DateTime T0 = new(2026, 11, 15, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Statement_IsTheAcceptedV1Text()
    {
        ConsentStatement.Version.ShouldBe("consent.v1");
        ConsentStatement.Text.ShouldBe("This is my own voice. I agree that StemStudio uses this recording on this computer to convert generated vocals into my voice. I can delete it at any time.");
        ConsentStatement.TextNl.ShouldBe("Dit is mijn eigen stem. Ik ga ermee akkoord dat StemStudio deze opname op deze computer gebruikt om gegenereerde zang om te zetten naar mijn stem. Ik kan de opname op elk moment verwijderen.");
        ConsentStatement.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ConsentStatement.Text))));
    }

    [Theory]
    [InlineData(false, "consent.v1", "Pat", ConsentProblem.Required, "consentConfirmed")]
    [InlineData(true, "consent.v0", "Pat", ConsentProblem.Outdated, "consentStatementVersion")]
    [InlineData(true, null, "Pat", ConsentProblem.Outdated, "consentStatementVersion")]
    [InlineData(true, "consent.v1", " ", ConsentProblem.Invalid, "personName")]
    public void Check_RefusesMissingOutdatedOrNamelessConsent(bool confirmed, string? version, string name, ConsentProblem problem, string field)
    {
        var outcome = ConsentRules.Check(confirmed, version, name, T0);

        (outcome.Problem, outcome.Consent).ShouldBe((problem, null));
        outcome.Errors.Single().Field.ShouldBe(field);
    }

    [Fact]
    public void Check_RecordsVersionHashNameAndTime()
    {
        var outcome = ConsentRules.Check(true, "consent.v1", " Pat Example ", T0);

        outcome.Problem.ShouldBe(ConsentProblem.None);
        outcome.Consent.ShouldBe(new VoiceConsent("consent.v1", ConsentStatement.Sha256, "Pat Example", T0));
        outcome.Consent!.Confirmed.ShouldBeTrue();
        ConsentRules.Check(true, "consent.v1", new string('p', 81), T0).Problem.ShouldBe(ConsentProblem.Invalid);
    }

    [Fact]
    public void Create_StoresConsentAndSample()
    {
        var profile = VoiceProfile.Create(Guid.NewGuid(), Draft(" My voice "), T0);

        (profile.DisplayName, profile.NormalizedName, profile.CreatedAt).ShouldBe(("My voice", "MY VOICE", T0));
        profile.Consent.PersonName.ShouldBe("Pat Example");
        (profile.SampleSource, profile.SamplePath, profile.SamplePcmSha256, profile.SampleFileSha256).ShouldBe((VoiceSampleSource.Recorded, "voices/x/sample.flac", new string('a', 64), new string('b', 64)));
        (profile.DurationSeconds, profile.VoicedRatio).ShouldBe((21.4, 0.78));
    }

    [Theory]
    [InlineData("", 20, 0.8, "displayName")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901", 20, 0.8, "displayName")]
    [InlineData("v", 9.9, 0.8, "sample")]
    [InlineData("v", 60.1, 0.8, "sample")]
    [InlineData("v", 20, 0.49, "sample")]
    public void Create_RejectsInvalidProfiles(string name, double duration, double voiced, string field) =>
        Should.Throw<DomainValidationException>(() => VoiceProfile.Create(Guid.NewGuid(), Draft(name) with { Sample = Sample() with { DurationSeconds = duration, VoicedRatio = voiced } }, T0))
            .Errors.ShouldContain(e => e.Field == field);

    [Fact]
    public void Naming_NormalisesForUniqueness() => VoiceNaming.Normalize(" My Voice ").ShouldBe("MY VOICE");

    private static VoiceProfileDraft Draft(string name) => new(name, new VoiceConsent("consent.v1", ConsentStatement.Sha256, "Pat Example", T0), Sample());

    private static VoiceSample Sample() => new(VoiceSampleSource.Recorded, "voices/x/sample.flac", new string('a', 64), new string('b', 64), 21.4, 0.78);
}
