using Microsoft.AspNetCore.Http.HttpResults;
using StemStudio.Api.Generations;
using StemStudio.Api.Generator;
using StemStudio.Contracts.Generation;
using StemStudio.Domain.Voices;

namespace StemStudio.Api.Voices;

/// <summary>Problem responses of the voice endpoints (backend 1.9.0 <c>ConsentProblem</c>, <c>VoiceConflict</c>, <c>SampleRejected</c>).</summary>
public static class VoiceProblems
{
    /// <summary>404 for an unknown profile.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The problem.</returns>
    public static IResult NotFound(Guid id) => ApiProblems.NotFound($"Voice profile {id} does not exist.");

    /// <summary>400 <c>consent-required</c>, <c>consent-outdated</c> or a plain validation problem.</summary>
    /// <param name="outcome">The consent outcome.</param>
    /// <returns>The problem.</returns>
    public static IResult Consent(ConsentOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var errors = outcome.Errors.ToDictionary(error => error.Field, error => new[] { error.Message }, StringComparer.Ordinal);
        return outcome.Problem switch
        {
            ConsentProblem.Required => TypedResults.ValidationProblem(errors, title: "Consent required", type: "consent-required"),
            ConsentProblem.Outdated => TypedResults.ValidationProblem(errors, title: "Consent statement changed", type: "consent-outdated"),
            _ => TypedResults.ValidationProblem(errors),
        };
    }

    /// <summary>409 <c>voice-name-taken</c>.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The problem.</returns>
    public static IResult NameTaken(string name) => Typed(StatusCodes.Status409Conflict, "voice-name-taken", "Voice name taken", $"A voice profile named \"{name}\" already exists.");

    /// <summary>409 <c>voice-not-installed</c> or <c>voice-unavailable</c>.</summary>
    /// <param name="readiness">The singer readiness.</param>
    /// <returns>The problem.</returns>
    public static IResult NotReady(GenerationReadiness readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        return readiness.State == GenerationAvailabilityDto.NotInstalled
            ? Typed(StatusCodes.Status409Conflict, "voice-not-installed", "Voice features not installed", SingerAvailability.SingerNotInstalledReason)
            : Typed(StatusCodes.Status409Conflict, "voice-unavailable", "Voice features unavailable", readiness.Reason ?? "The singer has no usable GPU.");
    }

    /// <summary>409 <c>no-vocals</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult NoVocals() => Typed(StatusCodes.Status409Conflict, "no-vocals", "No AI vocal to convert", "Only completed song takes with an AI voice can be converted.");

    /// <summary>413 for a sample above 20 MiB.</summary>
    /// <returns>The problem.</returns>
    public static IResult SampleTooLarge() =>
        TypedResults.Problem("The recording can be at most 20 MiB (20,971,520 bytes).", statusCode: StatusCodes.Status413PayloadTooLarge, title: "File too large");

    /// <summary>422 <c>library-audio</c>.</summary>
    /// <returns>The problem.</returns>
    public static IResult LibraryAudio() => Typed(
        StatusCodes.Status422UnprocessableEntity, "library-audio", "Not an own recording", "This file is audio from your StemStudio library. Record or upload your own voice instead.");

    /// <summary>422 <c>too-short</c>, <c>too-long</c> or <c>unvoiced</c> from the singer's check.</summary>
    /// <param name="check">The check.</param>
    /// <returns>The problem.</returns>
    public static IResult Rejected(GeneratorSampleCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        var (type, title) = check.Reason switch
        {
            "tooShort" => ("too-short", "Recording too short"),
            "tooLong" => ("too-long", "Recording too long"),
            _ => ("unvoiced", "Not enough singing"),
        };
        return Typed(StatusCodes.Status422UnprocessableEntity, type, title, check.Message ?? "The recording cannot be used; record 10 to 60 seconds of your own singing.");
    }

    private static ProblemHttpResult Typed(int status, string type, string title, string detail) => TypedResults.Problem(detail, statusCode: status, title: title, type: type);
}
