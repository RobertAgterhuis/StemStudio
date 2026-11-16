using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tools.Providers;

/// <summary>Shared builders for the feature providers (descriptor defaults, projection helpers, undo actions).</summary>
internal static class ToolDefinitions
{
    /// <summary>Audio length assumed for estimates when a track reports no duration (minutes).</summary>
    public const double DefaultAudioMinutes = 4;

    /// <summary>The <c>trackId</c> parameter of the track tools.</summary>
    public static ToolParameter TrackId { get; } = Param.Uuid("trackId").Describe("Track id from search_library.");

    /// <summary>Contract JSON of DTO values (camelCase, enum member names).</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) { Encoder = CanonicalJson.Encoder };

    /// <summary>A descriptor with the defaults (no undo, core group, always available); callers set the rest with <c>with</c>.</summary>
    public static AgentToolDescriptor Tool(string feature, string name, ToolKindDto kind, ToolRiskDto risk) => new()
    {
        Name = name,
        Feature = feature,
        Kind = kind,
        Risk = risk,
        UndoKind = UndoKindDto.None,
        Group = ToolGroup.Core,
        Availability = ToolAvailability.Always,
        Title = name,
        Description = name,
        Schema = ToolSchema.Empty,
    };

    /// <summary>The contract name of an enum value (e.g. <c>completed</c>).</summary>
    public static string Name<T>(T value)
        where T : struct, Enum => JsonSerializer.Serialize(value, Json).Trim('"');

    /// <summary>An undo action with canonical arguments.</summary>
    public static UndoAction Undo(string tool, JsonObject arguments) => new() { Tool = tool, Arguments = CanonicalJson.Canonicalize(arguments.ToJsonString(Json)) };

    /// <summary>An undo call that applies only while the mix is unchanged since the step (<c>updatedAt</c> guard).</summary>
    /// <param name="tool">Inverse or compensating tool.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="mix">The mix as the step left it.</param>
    /// <returns>The undo call.</returns>
    public static UndoAction MixUndo(string tool, JsonObject arguments, StemStudio.Contracts.Mixes.MixDto mix)
    {
        arguments[UndoGuard.Property] = new JsonObject { ["mixId"] = mix.Id.ToString(), ["updatedAt"] = mix.UpdatedAt };
        return Undo(tool, arguments);
    }

    /// <summary>The jobRef projection of a job.</summary>
    public static JsonObject JobRef(JobRef job) => new()
    {
        ["trackId"] = job.TrackId?.ToString(),
        ["jobId"] = job.JobId,
        ["phase"] = Name(job.Phase),
    };

    /// <summary>External text as an untrusted field (null when empty).</summary>
    public static string? Untrusted(string? text) => ResultProjector.Untrusted(text, 300);

    /// <summary>An external error as a nested untrusted object, so it taints the run (null without an error).</summary>
    public static JsonObject? UntrustedError(string? error) =>
        Untrusted(error) is { } text ? new JsonObject { [ResultProjector.UntrustedField] = text } : null;

    /// <summary>Lower case without diacritics (search filter).</summary>
    public static string Fold(string? text)
    {
        var builder = new StringBuilder();
        foreach (var c in (text ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>Linear volume (0-1) to dB, floor -60, one decimal.</summary>
    public static double GainDb(double volume) => volume <= 0 ? -60 : Math.Round(Math.Max(-60, 20 * Math.Log10(volume)), 1);
}
