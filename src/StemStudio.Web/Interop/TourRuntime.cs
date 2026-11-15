namespace StemStudio.Web.Interop;

/// <summary>A tour with texts already resolved by .NET (web-interop.md 2 <c>TourRuntime</c>).</summary>
/// <param name="Id">Tour id.</param>
/// <param name="Version">Tour content version.</param>
/// <param name="ReducedMotion">True disables popover animation.</param>
/// <param name="Steps">Steps in order.</param>
/// <param name="Labels">Button and progress labels.</param>
public sealed record TourRuntime(
    string Id,
    int Version,
    bool ReducedMotion,
    IReadOnlyList<TourRuntimeStep> Steps,
    TourLabels Labels);

/// <summary>One resolved tour step.</summary>
/// <param name="Target"><c>data-help-id</c> of the target element.</param>
/// <param name="Title">Popover title.</param>
/// <param name="Text">Popover text.</param>
public sealed record TourRuntimeStep(string Target, string Title, string Text);

/// <summary>Labels of the tour popover.</summary>
/// <param name="Next">"Next" button.</param>
/// <param name="Previous">"Previous" button.</param>
/// <param name="Done">"Done" button on the last step.</param>
/// <param name="Close">Accessible name of the close button.</param>
/// <param name="Progress">Progress template, e.g. <c>{{current}} of {{total}}</c>.</param>
public sealed record TourLabels(string Next, string Previous, string Done, string Close, string Progress);
