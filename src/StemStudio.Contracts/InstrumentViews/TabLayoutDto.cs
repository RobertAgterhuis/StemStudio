using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Tablature layout (backend-api 1.15.0, feature 016).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TabLayoutDto>))]
public enum TabLayoutDto
{
    /// <summary>Notation staff plus a TAB staff.</summary>
    [JsonStringEnumMemberName("notation-and-tab")]
    NotationAndTab,

    /// <summary>TAB staff only.</summary>
    [JsonStringEnumMemberName("tab-only")]
    TabOnly,
}
