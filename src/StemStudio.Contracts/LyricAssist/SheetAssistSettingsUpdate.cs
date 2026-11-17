using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>
/// Body of <c>PUT /api/lyrics/{lyricsId}/assist-settings</c> (schema <c>SheetAssistSettingsUpdate</c>). Absent members keep their
/// value; <c>rhythmTarget: null</c> clears the target, so <see cref="RhythmTargetSpecified"/> tells "absent" from "null".
/// </summary>
public sealed record SheetAssistSettingsUpdate
{
    private readonly RhythmTargetDto? _rhythmTarget;

    /// <summary>New sensitivity; null (not written) keeps it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SensitivityDto? Sensitivity { get; init; }

    /// <summary>New rhythm target; null clears it when <see cref="RhythmTargetSpecified"/>. The Web client always writes it.</summary>
    public RhythmTargetDto? RhythmTarget
    {
        get => _rhythmTarget;
        init
        {
            _rhythmTarget = value;
            RhythmTargetSpecified = true;
        }
    }

    /// <summary>True when the request carried <c>rhythmTarget</c> (also as null); false when it was absent.</summary>
    [JsonIgnore]
    public bool RhythmTargetSpecified { get; private init; }
}
