using StemStudio.Contracts.Insights;

namespace StemStudio.Web.Insights;

/// <summary>What the button of a quality tip starts (020 US2, FR-005): the page that shows the tips implements it.</summary>
public interface ITipActions
{
    /// <summary>Runs the tip's action.</summary>
    /// <param name="action">The action of the tip.</param>
    /// <returns>A task that completes when the action was started.</returns>
    Task RunAsync(TipActionDto action);
}
