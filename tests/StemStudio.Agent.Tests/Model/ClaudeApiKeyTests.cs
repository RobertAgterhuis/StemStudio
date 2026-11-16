using System.Security.Cryptography;
using System.Text;
using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;

namespace StemStudio.Agent.Tests.Model;

/// <summary>
/// <see cref="ClaudeApiKey"/> (011 follow-up claude-key T007, CK-FR-006, CK-FR-009): trimming, every format rule, the last four
/// characters, the SHA-256 fingerprint and the redaction. Keys are built at run time from <see cref="ClaudeKeySentinels"/>; no
/// key-shaped literal is written (research R-07).
/// </summary>
public sealed class ClaudeApiKeyTests
{
    private static readonly string Prefix = ClaudeKeySentinels.Prefix;

    [Fact]
    public void Parse_AcceptsAWellFormedSentinel()
    {
        var result = ClaudeApiKey.Parse(ClaudeKeySentinels.Stored);

        result.IsValid.ShouldBeTrue();
        result.Key.Reveal().ShouldBe(ClaudeKeySentinels.Stored);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Parse_TrimsSurroundingWhitespace()
    {
        var result = ClaudeApiKey.Parse("  \t" + ClaudeKeySentinels.Environment + "\r\n ");

        result.IsValid.ShouldBeTrue();
        result.Key.Reveal().ShouldBe(ClaudeKeySentinels.Environment);
        result.Key.LastFour.ShouldBe("ENV1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Parse_RefusesNothingAsEmpty(string? value) =>
        ClaudeApiKey.Parse(value).Error.ShouldBe(ClaudeKeyInvalidReasons.Empty);

    [Theory]
    [InlineData("sk-")]
    [InlineData("SK-ANT-")]
    [InlineData("xx-ant-")]
    [InlineData("sk-ant_")]
    public void Parse_RefusesAnotherPrefix(string otherPrefix) =>
        ClaudeApiKey.Parse(otherPrefix + new string('a', 60)).Error.ShouldBe(ClaudeKeyInvalidReasons.Prefix);

    [Fact]
    public void Parse_RefusesAValueWithoutAnyPrefix() =>
        ClaudeApiKey.Parse(new string('a', 100)).Error.ShouldBe(ClaudeKeyInvalidReasons.Prefix);

    [Theory]
    [InlineData(39, ClaudeKeyInvalidReasons.Length)]
    [InlineData(40, null)]
    [InlineData(256, null)]
    [InlineData(257, ClaudeKeyInvalidReasons.Length)]
    public void Parse_ChecksTheLengthAfterTrimming(int length, string? expected)
    {
        var value = OfLength(length);

        var result = ClaudeApiKey.Parse("  " + value + "  ");

        result.Error.ShouldBe(expected);
        if (expected is null)
        {
            result.Key!.Reveal().Length.ShouldBe(length);
        }
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData(".")]
    [InlineData("+")]
    [InlineData("/")]
    [InlineData("=")]
    [InlineData("é")]
    [InlineData(" ")]
    public void Parse_RefusesCharactersOutsideTheKeyAlphabet(string inner)
    {
        var value = string.Concat(Prefix, "api03-", new string('a', 20), inner, new string('b', 20));

        ClaudeApiKey.Parse(value).Error.ShouldBe(ClaudeKeyInvalidReasons.Characters);
    }

    [Fact]
    public void Parse_AcceptsTheWholeKeyAlphabet()
    {
        var value = string.Concat(Prefix, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

        ClaudeApiKey.Parse(value).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Parse_RefusesAnAdminKey() =>
        ClaudeApiKey.Parse(ClaudeKeySentinels.Admin).Error.ShouldBe(ClaudeKeyInvalidReasons.AdminKey);

    [Fact]
    public void Parse_RefusesAnAdminKeyInAnyCase() =>
        ClaudeApiKey.Parse(string.Concat(Prefix, "ADMIN01-", new string('a', 60))).Error.ShouldBe(ClaudeKeyInvalidReasons.AdminKey);

    [Fact]
    public void Parse_AcceptsAKeyThatOnlyContainsAdminLater() =>
        ClaudeApiKey.Parse(string.Concat(Prefix, "api03-admin", new string('a', 60))).IsValid.ShouldBeTrue();

    [Fact]
    public void Parse_ResultNeverPrintsTheValue()
    {
        var valid = ClaudeApiKey.Parse(ClaudeKeySentinels.Stored);
        var invalid = ClaudeApiKey.Parse(ClaudeKeySentinels.Stored + " x");

        invalid.Error.ShouldBe(ClaudeKeyInvalidReasons.Characters);
        foreach (var text in new[] { valid.ToString(), invalid.ToString(), valid.Key!.ToString(), $"{valid.Key}" })
        {
            foreach (var form in ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored))
            {
                text.ShouldNotContain(form);
            }
        }
    }

    [Fact]
    public void LastFour_IsTheEndOfTheTrimmedKey()
    {
        ClaudeApiKey.Parse(ClaudeKeySentinels.Stored).Key!.LastFour.ShouldBe("STO1");
        ClaudeApiKey.Parse(ClaudeKeySentinels.Build("q", "a-_9") + "  ").Key!.LastFour.ShouldBe("a-_9");
    }

    [Fact]
    public void Fingerprint_IsTheLowerCaseSha256HexOfTheKey()
    {
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ClaudeKeySentinels.Stored)));

        var key = ClaudeApiKey.Parse(" " + ClaudeKeySentinels.Stored).Key!;

        key.Fingerprint.ShouldBe(expected);
        key.Fingerprint.Length.ShouldBe(64);
        key.Fingerprint.ShouldNotContain(ClaudeKeySentinels.Stored);
    }

    [Fact]
    public void Fingerprint_DiffersPerKeyAndIsStable()
    {
        var first = ClaudeApiKey.Parse(ClaudeKeySentinels.Stored).Key!;
        var again = ClaudeApiKey.Parse(ClaudeKeySentinels.Stored).Key!;
        var other = ClaudeApiKey.Parse(ClaudeKeySentinels.Replacement).Key!;

        first.Fingerprint.ShouldBe(again.Fingerprint);
        first.Fingerprint.ShouldNotBe(other.Fingerprint);
    }

    [Fact]
    public void ToString_IsRedacted() =>
        ClaudeApiKey.Parse(ClaudeKeySentinels.Stored).Key!.ToString().ShouldBe(ClaudeApiKey.Redacted);

    [Fact]
    public void Reveal_IsTheOnlyPublicMemberReturningTheValue()
    {
        var key = ClaudeApiKey.Parse(ClaudeKeySentinels.Stored).Key!;
        var type = typeof(ClaudeApiKey);

        foreach (var property in type.GetProperties())
        {
            (property.GetValue(key) as string).ShouldNotBe(ClaudeKeySentinels.Stored, property.Name);
        }

        type.GetMethods().Where(m => m.ReturnType == typeof(string) && m.GetParameters().Length == 0 && !m.IsStatic)
            .Select(m => m.Name).Order().ShouldBe(["Reveal", "ToString"]);
        key.Reveal().ShouldBe(ClaudeKeySentinels.Stored);
    }

    [Fact]
    public void ClaudeApiKey_UsesReferenceEqualityOnly()
    {
        var first = ClaudeApiKey.Parse(ClaudeKeySentinels.Stored).Key!;
        var again = ClaudeApiKey.Parse(ClaudeKeySentinels.Stored).Key!;

        first.Equals(again).ShouldBeFalse();
        typeof(ClaudeApiKey).GetMethod(nameof(Equals), [typeof(object)])!.DeclaringType.ShouldBe(typeof(object));
    }

    private static string OfLength(int length) => string.Concat(Prefix, new string('k', length - Prefix.Length));
}
