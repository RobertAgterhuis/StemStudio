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
        RequireChannels(errors, dto.Channels ?? []);
        RequireDetail(errors, dto);
        return errors.Count == 0 ? (MixMapper.ToDomain(dto), errors) : (null, errors);
    }

    /// <summary>The 400 problem for Domain errors; <c>trackId</c> stays at the root, everything else gets the prefix.</summary>
    /// <param name="errors">Errors relative to the settings.</param>
    /// <param name="prefix">Path of the settings in the request body (<c>settings</c> for create, empty for update).</param>
    /// <returns>The problem.</returns>
    public static IResult Problem(IEnumerable<DomainError> errors, string prefix) =>
        ApiProblems.Validation(errors.Select(error => error.Field == "trackId" || prefix.Length == 0 ? error : error with { Field = $"{prefix}.{error.Field}" }), string.Empty);

    private static void RequireChannels(List<DomainError> errors, IReadOnlyList<ChannelSettingsDto> channels)
    {
        for (var i = 0; i < channels.Count; i++)
        {
            var field = string.Create(CultureInfo.InvariantCulture, $"channels[{i}]");
            Require(errors, channels[i] is not null, field);
            Require(errors, channels[i] is null || channels[i].Stem is not null, field + ".stem");
        }
    }

    /// <summary>Absent or null detail lists mean "empty" (mixes saved with 1.1.0); null entries are missing values.</summary>
    private static void RequireDetail(List<DomainError> errors, MixSettingsDto dto)
    {
        var expanded = dto.Expanded ?? [];
        for (var i = 0; i < expanded.Count; i++)
        {
            Require(errors, expanded[i] is not null, string.Create(CultureInfo.InvariantCulture, $"expanded[{i}]"));
        }

        var channels = dto.DetailChannels ?? [];
        for (var i = 0; i < channels.Count; i++)
        {
            var field = string.Create(CultureInfo.InvariantCulture, $"detailChannels[{i}]");
            Require(errors, channels[i] is { Parent: not null, Stem: not null }, field);
        }
    }

    private static void Require(List<DomainError> errors, bool present, string field)
    {
        if (!present)
        {
            errors.Add(new DomainError(field, Required));
        }
    }
}
