using System.Security.Cryptography;
using System.Text;
using StemStudio.Domain.LibraryCare;
using StemStudio.Domain.Voices;

namespace StemStudio.Domain.Tests.LibraryCare;

/// <summary>013 T015: embedded consent statements and the consent proof of an archived voice profile (data-model 5.4, FR-009, FR-016).</summary>
public sealed class ConsentProofTests
{
    private static readonly DateTime Archive = new(2026, 11, 16, 14, 22, 33, DateTimeKind.Utc);
    private static readonly string SampleHash = new('a', 64);

    private static ConsentProof Valid => new(ConsentStatement.Version, ConsentStatement.Sha256, "Alex", Archive.AddDays(-1), SampleHash);

    [Fact]
    public void Known_ReturnsTheV1TextAndItsHash()
    {
        var known = ConsentStatement.Known("consent.v1").ShouldNotBeNull();

        known.Text.ShouldBe(ConsentStatement.Text);
        known.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ConsentStatement.Text))));
    }

    [Theory]
    [InlineData("consent.v2")]
    [InlineData("consent.v0")]
    [InlineData("")]
    public void Known_UnknownVersionsAreNull(string version) => ConsentStatement.Known(version).ShouldBeNull();

    [Fact]
    public void Check_AValidProofHasNoProblem() =>
        ConsentProofRules.Check(Valid, Valid, Archive, SampleHash).ShouldBe(ConsentProofProblem.None);

    [Fact]
    public void Check_ConsentAtTheArchiveTimeIsValid() =>
        ConsentProofRules.Check(Valid with { ConsentedAt = Archive }, Valid with { ConsentedAt = Archive }, Archive, SampleHash).ShouldBe(ConsentProofProblem.None);

    [Fact]
    public void Check_TrimmedNamesAreCompared()
    {
        var padded = Valid with { PersonName = "  Alex " };

        ConsentProofRules.Check(padded, padded, Archive, SampleHash).ShouldBe(ConsentProofProblem.None);
    }

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Check_FindsTheFirstProblem(ConsentProof manifest, ConsentProof? database, string? archivedSample, ConsentProofProblem expected) =>
        ConsentProofRules.Check(manifest, database, Archive, archivedSample).ShouldBe(expected);

    public static TheoryData<ConsentProof, ConsentProof?, string?, ConsentProofProblem> Invalid() => new()
    {
        { Valid with { StatementVersion = "consent.v9" }, Valid with { StatementVersion = "consent.v9" }, SampleHash, ConsentProofProblem.UnknownStatement },
        { Valid with { StatementSha256 = new string('0', 64) }, Valid with { StatementSha256 = new string('0', 64) }, SampleHash, ConsentProofProblem.StatementMismatch },
        { Valid with { PersonName = " " }, Valid with { PersonName = " " }, SampleHash, ConsentProofProblem.PersonName },
        { Valid with { PersonName = new string('x', 81) }, Valid with { PersonName = new string('x', 81) }, SampleHash, ConsentProofProblem.PersonName },
        { Valid with { ConsentedAt = Archive.AddSeconds(1) }, Valid with { ConsentedAt = Archive.AddSeconds(1) }, SampleHash, ConsentProofProblem.ConsentedAfterArchive },
        { Valid, Valid, null, ConsentProofProblem.SampleMissing },
        { Valid, Valid, new string('b', 64), ConsentProofProblem.SampleMismatch },
        { Valid, null, SampleHash, ConsentProofProblem.DatabaseDiffers },
        { Valid, Valid with { PersonName = "Sam" }, SampleHash, ConsentProofProblem.DatabaseDiffers },
        { Valid, Valid with { ConsentedAt = Archive.AddDays(-2) }, SampleHash, ConsentProofProblem.DatabaseDiffers },
        { Valid, Valid with { SampleSha256 = new string('c', 64) }, SampleHash, ConsentProofProblem.DatabaseDiffers },
    };

    [Theory]
    [InlineData(ConsentProofProblem.UnknownStatement)]
    [InlineData(ConsentProofProblem.StatementMismatch)]
    [InlineData(ConsentProofProblem.PersonName)]
    [InlineData(ConsentProofProblem.ConsentedAfterArchive)]
    [InlineData(ConsentProofProblem.SampleMissing)]
    [InlineData(ConsentProofProblem.SampleMismatch)]
    [InlineData(ConsentProofProblem.DatabaseDiffers)]
    public void Reason_IsReadable(ConsentProofProblem problem) => ConsentProofRules.Reason(problem).ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void Reason_OfNoProblemIsNull() => ConsentProofRules.Reason(ConsentProofProblem.None).ShouldBeNull();

    [Fact]
    public void StatementCheck_KnownVersionAndMatchingHash()
    {
        ConsentProofRules.IsStatementValid(ConsentStatement.Version, ConsentStatement.Sha256).ShouldBeTrue();
        ConsentProofRules.IsStatementValid(ConsentStatement.Version, new string('0', 64)).ShouldBeFalse();
        ConsentProofRules.IsStatementValid("consent.v2", ConsentStatement.Sha256).ShouldBeFalse();
    }

    [Fact]
    public void IsComplete_NeedsAKnownStatementAndAName()
    {
        var consent = new VoiceConsent(ConsentStatement.Version, ConsentStatement.Sha256, "Alex", Archive);

        ConsentProofRules.IsComplete(consent).ShouldBeTrue();
        ConsentProofRules.IsComplete(consent with { PersonName = "" }).ShouldBeFalse();
        ConsentProofRules.IsComplete(consent with { StatementSha256 = "x" }).ShouldBeFalse();
        ConsentProofRules.IsComplete(null).ShouldBeFalse();
    }

    [Fact]
    public void FromConsent_CopiesTheFiveFields()
    {
        var consent = new VoiceConsent(ConsentStatement.Version, ConsentStatement.Sha256, "Alex", Archive);

        ConsentProof.From(consent, SampleHash).ShouldBe(new ConsentProof(ConsentStatement.Version, ConsentStatement.Sha256, "Alex", Archive, SampleHash));
    }
}
