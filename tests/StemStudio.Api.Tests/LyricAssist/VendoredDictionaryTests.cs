using System.Security.Cryptography;
using System.Text.Json;
using WeCantSpell.Hunspell;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>
/// Skeleton tests (architect, 023 Phase 0, T006): the vendored dictionaries in
/// <c>src/StemStudio.Api/LyricAssist/Dictionaries/</c> are copied to the output unchanged (SHA-256 of <c>manifest.json</c>),
/// carry only allowed licences, and load with WeCantSpell.Hunspell and as CMUdict (research R-02, R-04, R-10).
/// </summary>
public sealed class VendoredDictionaryTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "LyricAssist", "Dictionaries");

    private static readonly string[] AllowedLicences = ["BSD-3-Clause", "SCOWL", "BSD-2-Clause"];

    private static readonly string[] ArpabetVowels = ["AA", "AE", "AH", "AO", "AW", "AY", "EH", "ER", "EY", "IH", "IY", "OW", "OY", "UH", "UW"];

    public static TheoryData<string> ManifestPaths => [.. Manifest().Select(entry => entry.Path)];

    [Theory]
    [MemberData(nameof(ManifestPaths))]
    public async Task ManifestFile_IsInTheOutputWithTheRecordedHash(string path)
    {
        var entry = Manifest().Single(e => e.Path == path);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(Root, path), TestContext.Current.CancellationToken);

        ((long)bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes))).ShouldBe((entry.Bytes, entry.Sha256));
    }

    [Fact]
    public void Manifest_NamesOnlyAllowedLicencesAndEveryDataFolder()
    {
        var entries = Manifest();

        entries.Select(e => e.Licence).Distinct().ShouldBeSubsetOf(AllowedLicences);
        entries.Select(e => e.Path.Split('/')[0]).Distinct().ShouldBe(["nl", "en_US", "en_GB", "cmudict"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("nl/nl.dic", "fiets", "fietsz")]
    [InlineData("en_US/en_US.dic", "color", "recieve")]
    [InlineData("en_GB/en_GB-ise.dic", "colour", "recieve")]
    [InlineData("en_GB/en_GB-ize.dic", "realize", "recieve")]
    public void Hunspell_LoadsTheDictionaryAndChecksWords(string dictionary, string known, string misspelled)
    {
        var words = WordList.CreateFromFiles(Path.Combine(Root, dictionary));

        var ct = TestContext.Current.CancellationToken;

        (words.Check(known, ct), words.Check(misspelled, ct)).ShouldBe((true, false));
    }

    [Fact]
    public async Task CmuDict_HasMoreThan100000EntriesAndHelloHasTwoVowels()
    {
        var lines = await File.ReadAllLinesAsync(Path.Combine(Root, "cmudict", "cmudict.dict"), TestContext.Current.CancellationToken);
        var hello = lines.Single(line => line.StartsWith("hello ", StringComparison.Ordinal)).Split(' ').Skip(1);

        lines.Length.ShouldBeGreaterThan(100_000);
        hello.Count(phone => ArpabetVowels.Contains(phone.TrimEnd('0', '1', '2'))).ShouldBe(2);
    }

    private static List<ManifestEntry> Manifest()
    {
        using var stream = File.OpenRead(Path.Combine(Root, "manifest.json"));
        using var document = JsonDocument.Parse(stream);
        return [.. document.RootElement.GetProperty("files").EnumerateArray().Select(ManifestEntry.From)];
    }

    private sealed record ManifestEntry(string Path, string Sha256, long Bytes, string Licence)
    {
        public static ManifestEntry From(JsonElement e) =>
            new(e.GetProperty("path").GetString()!, e.GetProperty("sha256").GetString()!, e.GetProperty("bytes").GetInt64(), e.GetProperty("licence").GetString()!);
    }
}
