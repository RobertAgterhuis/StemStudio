namespace StemStudio.Web.Interop;

/// <summary>Arguments of the JS callback <c>OnTourEnded(tourId, "completed" | "dismissed", lastStep)</c>.</summary>
/// <param name="TourId">Id of the tour that ended.</param>
/// <param name="Completed">True for <c>completed</c>, false for <c>dismissed</c>.</param>
/// <param name="LastStep">Zero-based index of the last step shown (0 when no step was visible).</param>
public sealed record TourEnded(string TourId, bool Completed, int LastStep);
