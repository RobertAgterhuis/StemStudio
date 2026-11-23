using System.Text.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Contracts.Insights;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>
/// Consumer-driven fakes of the 020 backend operations (backend-api 1.19.0): the JSON documents are the contract
/// examples verbatim (<c>ReadyQualityReport</c>, <c>BassHandoutOptions</c>); the builders derive variants from them.
/// </summary>
public static class InsightsData
{
    /// <summary>Id of the bass score in the <c>BassHandoutOptions</c> example.</summary>
    public static readonly Guid BassScoreId = Guid.Parse("2c3d4e5f-6071-4829-8a0b-c1d2e3f4a5b7");

    /// <summary>Id of a handout library item.</summary>
    public static readonly Guid HandoutId = Guid.Parse("5e6f7081-92a3-4b4c-8d5e-6f708192a3b4");

    /// <summary>Id of a setlist.</summary>
    public static readonly Guid SetlistId = Guid.Parse("3a4b5c6d-7e8f-4901-a2b3-c4d5e6f7a8b9");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary><c>getQualityReport</c> 200 example <c>ReadyQualityReport</c>.</summary>
    public const string ReadyReportJson = """
        {"status":"ready","progress":100,"error":null,"analyzerVersion":"1.0.0","measuredAt":"2026-11-16T10:00:15Z","outdated":false,
         "disclaimer":"These ratings are estimates made from the stems alone, without the original recording. They can be wrong.",
         "summary":{"good":3,"fair":2,"poor":0,"notPresent":1,"unknown":0},
         "stems":[
          {"channel":"other","parent":null,"rating":"fair","reason":"Some sound of guitar is in this stem.","previousRating":null,
           "bleed":{"level":"medium","index":0.19,"mainSource":"guitar","coOccurrence":false,"reliability":"validated"},
           "artefacts":{"level":"low","index":0.2,"kinds":[],"reliability":"validated"},
           "tips":[{"code":"try-detail-separation","text":"Split further (detail separation) may pull apart the instruments mixed into this stem.",
                    "action":{"kind":"split-detail","channel":"other","parent":"other","child":null}}]},
          {"channel":"piano","parent":null,"rating":"not-present","reason":"This instrument is probably not in this song; what you hear is spill from other instruments.",
           "previousRating":null,
           "bleed":{"level":"high","index":0.71,"mainSource":"guitar","coOccurrence":false,"reliability":"indicative"},
           "artefacts":{"level":"unknown","index":0.4,"kinds":["flicker"],"reliability":"indicative"},
           "tips":[{"code":"not-in-song","text":"Mute this stem; the instrument is probably not in this song.",
                    "action":{"kind":"mute","channel":"piano","parent":null,"child":null}}]}]}
        """;

    /// <summary><c>getHandoutOptions</c> 200 example <c>BassHandoutOptions</c>.</summary>
    public const string BassOptionsJson = """
        {"kind":"song","family":"bass","familyLabel":"Bass","songs":1,
         "ingredients":[
          {"code":"facts","status":"available","defaultIncluded":true,"text":"Tempo 120 BPM, 4/4, A minor","action":null},
          {"code":"form-chart","status":"available","defaultIncluded":true,"text":"7 sections","action":null},
          {"code":"chord-chart","status":"available","defaultIncluded":true,"text":"4 chords","action":null},
          {"code":"notation","status":"available","defaultIncluded":true,"text":"Bass score; tablature not generated yet","action":"create-tab"},
          {"code":"practice-notes","status":"missing","defaultIncluded":true,"text":"No notes yet","action":"write-notes"}],
         "notation":[{"assetId":"2c3d4e5f-6071-4829-8a0b-c1d2e3f4a5b7","title":"Bass - score","kind":"score","transposition":null,"defaultSelected":true,"songTitle":"Example Song"}],
         "transpositions":[],"defaultPaper":"a4","estimateSeconds":6}
        """;

    /// <summary>The ready report example.</summary>
    /// <returns>The report.</returns>
    public static QualityReportDto ReadyReport() => Parse<QualityReportDto>(ReadyReportJson);

    /// <summary>A report in another status, keeping the example stems unless <paramref name="stems"/> is false.</summary>
    /// <param name="status">Status.</param>
    /// <param name="progress">Progress 0..100.</param>
    /// <param name="stems">Keep the stems of the last ready report.</param>
    /// <returns>The report.</returns>
    public static QualityReportDto Report(QualityReportStatusDto status, int progress = 0, bool stems = true) => ReadyReport() with
    {
        Status = status,
        Progress = progress,
        Stems = stems ? ReadyReport().Stems : [],
        AnalyzerVersion = stems ? "1.0.0" : null,
        MeasuredAt = stems ? ReadyReport().MeasuredAt : null,
    };

    /// <summary>A never-made report (<c>status none</c>).</summary>
    /// <returns>The report.</returns>
    public static QualityReportDto NoReport() => Report(QualityReportStatusDto.None, stems: false) with
    {
        Summary = new QualitySummaryDto { Good = 0, Fair = 0, Poor = 0, NotPresent = 0, Unknown = 0 },
    };

    /// <summary>The ready report with extra stems appended.</summary>
    /// <param name="stems">The stems.</param>
    /// <returns>The report.</returns>
    public static QualityReportDto WithStems(params StemQualityViewDto[] stems) => ReadyReport() with { Stems = [.. ReadyReport().Stems, .. stems] };

    /// <summary>The <c>other</c> stem of the example with changes.</summary>
    /// <param name="channel">ChannelId.</param>
    /// <param name="rating">Rating.</param>
    /// <param name="parent">Parent stem of a detail stem.</param>
    /// <param name="tips">Tips; null keeps none.</param>
    /// <returns>The stem view.</returns>
    public static StemQualityViewDto Stem(string channel, QualityRatingDto rating, string? parent = null, params TipDto[] tips) =>
        ReadyReport().Stems[0] with { Channel = channel, Parent = parent, Rating = rating, Reason = $"Reason of {channel}.", Tips = tips };

    /// <summary>A tip.</summary>
    /// <param name="code">Code.</param>
    /// <param name="text">Text.</param>
    /// <param name="action">The action; null = advice only.</param>
    /// <returns>The tip.</returns>
    public static TipDto Tip(TipCodeDto code, string text, TipActionDto? action = null) => new() { Code = code, Text = text, Action = action };

    /// <summary>A tip action.</summary>
    /// <param name="kind">Kind.</param>
    /// <param name="channel">Channel the tip is about.</param>
    /// <param name="parent">Parent for detail actions.</param>
    /// <param name="child">Detail stem for <c>open-detail</c>.</param>
    /// <returns>The action.</returns>
    public static TipActionDto Action(TipActionKindDto kind, string channel, string? parent = null, string? child = null) =>
        new() { Kind = kind, Channel = channel, Parent = parent, Child = child };

    /// <summary>The bass options example.</summary>
    /// <returns>The options.</returns>
    public static HandoutOptionsDto BassOptions() => Parse<HandoutOptionsDto>(BassOptionsJson);

    /// <summary>Options of another family derived from the bass example.</summary>
    /// <param name="family">Family.</param>
    /// <param name="label">Family label.</param>
    /// <returns>The options.</returns>
    public static HandoutOptionsDto Options(HandoutFamilyDto family, string label) => BassOptions() with
    {
        Family = family,
        FamilyLabel = label,
        Transpositions = family == HandoutFamilyDto.Melodic
            ? [TranspositionDto.C, TranspositionDto.BFlat, TranspositionDto.EFlat, TranspositionDto.F]
            : [],
    };

    /// <summary>A queued handout item (<c>createHandout</c> 202).</summary>
    /// <param name="status">Status.</param>
    /// <param name="facts">Facts of a ready item.</param>
    /// <returns>The item.</returns>
    public static AssetDto Handout(AssetStatusDto status = AssetStatusDto.Queued, HandoutFactsDto? facts = null) => AssetData.Asset(HandoutId, status, "Never Gonna Give You Up - Bass handout") with
    {
        Kind = AssetKindDto.Handout,
        Audio = null,
        Files = status == AssetStatusDto.Ready
            ? [new AssetFileDto(AssetFileRoleDto.Pdf, "application/pdf", 120_000, new string('b', 64), ApiRoutes.AssetFile(HandoutId, "pdf"))]
            : [],
        Handout = facts,
    };

    /// <summary>Facts of a ready bass handout without tablature.</summary>
    /// <returns>The facts.</returns>
    public static HandoutFactsDto Facts() => new()
    {
        Kind = HandoutKindDto.Song,
        Family = HandoutFamilyDto.Bass,
        Transposition = null,
        Paper = PaperDto.A4,
        Pages = 4,
        Songs = 1,
        Included = [IngredientCodeDto.Facts, IngredientCodeDto.FormChart, IngredientCodeDto.ChordChart, IngredientCodeDto.Notation],
        Omitted = [new OmittedIngredientDto { Code = IngredientCodeDto.PracticeNotes, Reason = OmissionReasonDto.NotAvailable }],
        Warnings = [new HandoutWarningDto { Code = HandoutWarningCodeDto.NotationScaled, Text = "Notation pages were scaled to fit the paper." }],
        Outline = ["Never Gonna Give You Up", "Form", "Notation - Bass score"],
    };

    /// <summary>A practice note.</summary>
    /// <param name="family">Family.</param>
    /// <param name="text">Text.</param>
    /// <returns>The note.</returns>
    public static PracticeNoteDto Note(HandoutFamilyDto family, string text) => new() { Family = family, Text = text, UpdatedAt = TestData.Created };

    /// <summary>Serialises a DTO as the backend would.</summary>
    /// <typeparam name="T">DTO type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The JSON.</returns>
    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Json);

    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)!;

    /// <summary>The fixed disclaimer of the contract, for assertions.</summary>
    public static string Disclaimer => ContractLimits.QualityDisclaimer;
}
