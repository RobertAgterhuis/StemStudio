using System.Text.Json;
using System.Text.Json.Serialization;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Insights;

namespace StemStudio.Api.Worker;

/// <summary>Kind of a quality input (worker 1.11.0 <c>QualityInputKind</c>).</summary>
public enum WorkerQualityInputKind
{
    /// <summary>One of the six standard stems.</summary>
    Standard,

    /// <summary>A kept detail stem.</summary>
    Detail,

    /// <summary>The <c>rest</c> stem of a split parent.</summary>
    Rest,
}

/// <summary>One stem of a quality job (worker 1.11.0 <c>QualityInput</c>).</summary>
/// <param name="Channel">ChannelId.</param>
/// <param name="Path">Library path of the FLAC.</param>
/// <param name="Kind">Kind.</param>
public sealed record WorkerQualityInput(string Channel, string Path, WorkerQualityInputKind Kind);

/// <summary>The 002 metrics of a split parent (worker 1.11.0 <c>DetailParentInput</c>).</summary>
/// <param name="Parent">Parent stem.</param>
/// <param name="ModelResidualDb">Model residual, or null when unknown.</param>
/// <param name="ReconstructionErrorDb">Reconstruction error, or null when unknown.</param>
public sealed record WorkerDetailParentInput(string Parent, double? ModelResidualDb, double? ReconstructionErrorDb);

/// <summary>Titles for the worker's logs (worker 1.11.0 <c>QualityTitles</c>).</summary>
/// <param name="Title">Title (at most 200 characters).</param>
/// <param name="Artist">Artist (at most 200 characters), or null.</param>
public sealed record WorkerQualityTitles(string Title, string? Artist);

/// <summary>Body of <c>POST /v1/quality-jobs</c> (worker 1.11.0 <c>QualityJobRequest</c>).</summary>
/// <param name="Stems">The six standard stems first, then detail and rest stems.</param>
/// <param name="DetailParents">Split parents with their metrics.</param>
/// <param name="Titles">Titles.</param>
public sealed record WorkerQualityJobRequest(IReadOnlyList<WorkerQualityInput> Stems, IReadOnlyList<WorkerDetailParentInput> DetailParents, WorkerQualityTitles Titles);

/// <summary>Measures of one stem (worker 1.11.0 <c>StemQuality</c>).</summary>
/// <param name="Channel">ChannelId.</param>
/// <param name="Rating">Rating.</param>
/// <param name="RatingReason">Reason code.</param>
/// <param name="ActiveSeconds">Active seconds.</param>
/// <param name="ActiveShare">Active share 0..1.</param>
/// <param name="LevelDb">Level relative to the mix.</param>
/// <param name="Bleed">Bleed estimate.</param>
/// <param name="Artefacts">Artefact estimate.</param>
public sealed record WorkerStemQuality(
    string Channel, QualityRating Rating, QualityReason RatingReason, double ActiveSeconds, double ActiveShare, double LevelDb, BleedEstimate Bleed, ArtefactEstimate Artefacts)
{
    /// <summary>The domain form.</summary>
    /// <returns>The measures.</returns>
    public StemQuality ToDomain() => new(Channel, Rating, RatingReason, ActiveSeconds, ActiveShare, LevelDb, Bleed, Artefacts);
}

/// <summary>Result of a quality job (worker 1.11.0 <c>QualityResult</c>).</summary>
/// <param name="AnalyzerVersion">Analyzer version.</param>
/// <param name="DurationSeconds">Analysis time.</param>
/// <param name="Stems">Measures in request order.</param>
public sealed record WorkerQualityResult(string AnalyzerVersion, double DurationSeconds, IReadOnlyList<WorkerStemQuality> Stems);

/// <summary>A quality job (worker 1.11.0 <c>QualityJob</c>).</summary>
/// <param name="QualityJobId">Job id (32 hex).</param>
/// <param name="Status">Status.</param>
/// <param name="Stage"><c>loading</c>, <c>analysing</c>, <c>rating</c> or null.</param>
/// <param name="Progress">Progress 0..1.</param>
/// <param name="Error">Failure text.</param>
/// <param name="Result">Result when completed.</param>
public sealed record WorkerQualityJob(string QualityJobId, WorkerRenderJobState Status, string? Stage, double Progress, string? Error, WorkerQualityResult? Result);

/// <summary>Fonts of the handout renderer (worker 1.11.0).</summary>
/// <param name="Archivo">Archivo is packaged.</param>
/// <param name="Fallback">DejaVu Sans is installed.</param>
public sealed record WorkerHandoutFonts(bool Archivo, bool Fallback);

/// <summary>Renderer versions (worker 1.11.0).</summary>
/// <param name="Weasyprint">WeasyPrint version, or null when it cannot be imported.</param>
/// <param name="Pango">Pango version, or null.</param>
public sealed record WorkerHandoutRenderer(string? Weasyprint, string? Pango);

/// <summary><c>Health.insights</c> (worker 1.11.0 <c>InsightsHealth</c>).</summary>
/// <param name="Analyzer"><c>real</c> or <c>fake</c>.</param>
/// <param name="AnalyzerVersion">Current analyzer version.</param>
/// <param name="Renderer">Renderer versions.</param>
/// <param name="Fonts">Fonts.</param>
public sealed record WorkerInsightsHealth(string Analyzer, string AnalyzerVersion, WorkerHandoutRenderer Renderer, WorkerHandoutFonts Fonts);

/// <summary>Wire converters of the insights and handout jobs: their enums are kebab-case on the wire (worker 1.11.0).</summary>
public static class WorkerInsightsJson
{
    /// <summary>The converters, added before the general camelCase converter.</summary>
    public static IEnumerable<JsonConverter> Converters { get; } =
    [
        new JsonStringEnumConverter<QualityRating>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<QualityReason>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<QualityLevel>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<Reliability>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<ArtefactKind>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<WorkerQualityInputKind>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<HandoutKind>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<HandoutFamily>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<IngredientCode>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<Paper>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<NotationKind>(JsonNamingPolicy.KebabCaseLower),
        new JsonStringEnumConverter<HandoutWarningCode>(JsonNamingPolicy.KebabCaseLower),
    ];
}
