using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.TestDoubles.JsonSchema;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 013 Phase 0, T006): the shared <see cref="SchemaSubsetValidator"/> covers every keyword of
/// <c>backup-manifest.schema.json</c> (backup-archive 1.0.0), accepts a complete manifest and refuses the unsafe shapes
/// the writer and reader rules exclude. Backend T033 validates the writer's output with the same validator.
/// </summary>
public sealed class BackupManifestSchemaTests
{
    private const string Track = "0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f";
    private const string Profile = "7d3c2b1a-0f9e-4d8c-b7a6-5e4d3c2b1a09";

    private static readonly SchemaSubsetValidator Manifest =
        SchemaSubsetValidator.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "013", "backup-manifest.schema.json"));

    private static readonly string Hash = new('a', 64);

    [Fact]
    public void CompleteManifest_IsValid()
    {
        Manifest.Validate(Element(Valid())).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("files/../../etc/passwd")]
    [InlineData("files//tracks/x")]
    [InlineData("/etc/passwd")]
    [InlineData("files\\tracks\\x.flac")]
    [InlineData("db/other.db")]
    public void UnsafeEntryPath_IsRefused(string path)
    {
        var manifest = Valid();
        manifest["entries"]![1]!["path"] = path;

        Manifest.Validate(Element(manifest)).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("format", "\"other\"")]
    [InlineData("formatVersion", "\"2.0.0\"")]
    [InlineData("encryption", "\"aes\"")]
    [InlineData("hashAlgorithm", "\"md5\"")]
    [InlineData("backupId", "\"not-a-guid\"")]
    public void WrongConstantsAndPatterns_AreRefused(string member, string json)
    {
        var manifest = Valid();
        manifest[member] = JsonNode.Parse(json);

        Manifest.Validate(Element(manifest)).ShouldNotBeEmpty();
    }

    [Fact]
    public void DuplicateIncludedCategoryAndTooManyEntries_AreRefused()
    {
        var manifest = Valid();
        manifest["contents"]!["included"] = new JsonArray("stems", "stems");
        manifest["totals"]!["entries"] = 2_000_001;

        Manifest.Validate(Element(manifest)).Count.ShouldBe(2);
    }

    [Fact]
    public void UnknownTopLevelMember_IsAcceptedForLaterMinors()
    {
        var manifest = Valid();
        manifest["laterMinorMember"] = 1;

        Manifest.Validate(Element(manifest)).ShouldBeEmpty();
    }

    private static JsonElement Element(JsonNode node) => JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();

    private static JsonObject Valid() => JsonNode.Parse($$"""
        {
          "format": "stemstudio-backup", "formatVersion": "1.0.0", "backupId": "5b0f6a3e-2c1d-4e8f-9a7b-1c2d3e4f5a6b",
          "createdAt": "2026-11-16T14:22:33Z",
          "app": { "name": "StemStudio", "version": "1.12.0+5ae61cf", "backendApi": "1.12.0" },
          "schema": { "provider": "sqlite", "lastMigration": "20261115200748_AddLyricsAndVoices",
                      "migrations": ["20261114215330_Initial", "20261115200748_AddLyricsAndVoices"] },
          "contents": { "included": ["database", "stems", "voiceProfiles"], "excluded": [ { "category": "detailStems", "reason": "option" } ] },
          "counts": { "tracks": 1, "mixes": 0, "assets": 0, "generations": 0, "styles": 0, "lyricsSheets": 0, "voiceProfiles": 1, "artists": 1 },
          "totals": { "entries": 3, "bytes": 300 },
          "hashAlgorithm": "sha-256", "encryption": "none",
          "entries": [
            { "path": "db/stemstudio.db", "category": "database", "sizeBytes": 100, "sha256": "{{Hash}}" },
            { "path": "files/tracks/{{Track}}/stems/vocals.flac", "category": "stems", "sizeBytes": 100, "sha256": "{{Hash}}",
              "entity": { "type": "track", "id": "{{Track}}" } },
            { "path": "files/voices/{{Profile}}/sample.flac", "category": "voiceProfiles", "sizeBytes": 100, "sha256": "{{Hash}}" }
          ],
          "voiceProfiles": [
            { "id": "{{Profile}}", "displayName": "My voice",
              "consent": { "statementVersion": "consent.v1", "statementSha256": "{{Hash}}", "personName": "Alex", "consentedAt": "2026-11-15T21:03:00Z" },
              "samplePath": "files/voices/{{Profile}}/sample.flac", "sampleSha256": "{{Hash}}" }
          ],
          "warnings": [ { "code": "missing-file", "message": "1 file was missing." } ]
        }
        """)!.AsObject();
}
