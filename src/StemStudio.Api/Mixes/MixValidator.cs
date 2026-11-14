using System.Globalization;
using StemStudio.Contracts.Mixes;
using StemStudio.Domain;
using StemStudio.Domain.Mixes;

namespace StemStudio.Api.Mixes;

/// <summary>
/// Checks that the required parts of a mix body are present and converts it to Domain settings; value rules live in
/// the Domain (<see cref="MixSettings.Validate"/>). Error keys are JSON paths of the request body.
/// </summary>
public static class MixValidator
{
    /// <summary>Message for a missing required value.</summary>
    public const string Required = "This value is required.";

    /// <summary>Converts a body to Domain settings.</summary>
    /// <param name="dto">The deserialised body part.</param>
    /// <returns>The settings, or null with the missing parts.</returns>
    public static (MixSettings? Settings, IReadOnlyList<DomainError> Errors) ToDomain(MixSettingsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var errors = new List<DomainError>();
        Require(errors, dto.Name is not null, "name");
        Require(errors, dto.Metronome is not null, "metronome");
        Require(errors, dto.Channels is not null, "channels");
        for (var i = 0; i < (dto.Channels?.Count ?? 0); i++)
        {
            var field = string.Create(CultureInfo.InvariantCulture, $"channels[{i}]");
            Require(errors, dto.Channels![i] is not null, field);
            Require(errors, dto.Channels[i] is null || dto.Channels[i].Stem is not null, field + ".stem");
        }

        return errors.Count == 0 ? (MixMapper.ToDomain(dto), errors) : (null, errors);
    }

    /// <summary>The 400 problem for Domain errors; <c>trackId</c> stays at the root, everything else gets the prefix.</summary>
    /// <param name="errors">Errors relative to the settings.</param>
    /// <param name="prefix">Path of the settings in the request body (<c>settings</c> for create, empty for update).</param>
    /// <returns>The problem.</returns>
    public static IResult Problem(IEnumerable<DomainError> errors, string prefix) =>
        ApiProblems.Validation(errors.Select(error => error.Field == "trackId" || prefix.Length == 0 ? error : error with { Field = $"{prefix}.{error.Field}" }), string.Empty);

    private static void Require(List<DomainError> errors, bool present, string field)
    {
        if (!present)
        {
            errors.Add(new DomainError(field, Required));
        }
    }
}
