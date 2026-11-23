using StemStudio.Contracts.Insights;

namespace StemStudio.Web.Insights;

/// <summary>Mixer links of the tip actions (020 Q2/Q3): <c>?mute=</c> mutes the stem unsaved, <c>?expand=</c> expands the split parent.</summary>
public static class TipLinks
{
    /// <summary>The mixer link of a <c>mute</c> or <c>open-detail</c> action.</summary>
    /// <param name="trackId">Track.</param>
    /// <param name="action">The action.</param>
    /// <returns>The relative link.</returns>
    public static string Mixer(Guid trackId, TipActionDto action) => throw new NotImplementedException();
}
