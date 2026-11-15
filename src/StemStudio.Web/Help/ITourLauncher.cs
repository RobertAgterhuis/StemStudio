namespace StemStudio.Web.Help;

/// <summary>Starts a guided tour by id (welcome panel, tour offers, help panel tour list).</summary>
public interface ITourLauncher
{
    /// <summary>Starts the tour; unknown ids and tours without a visible step do nothing.</summary>
    /// <param name="tourId">The tour id, e.g. <c>import-basics</c>.</param>
    /// <returns>A task that completes when the tour has started (not when it ends).</returns>
    Task StartAsync(string tourId);
}
