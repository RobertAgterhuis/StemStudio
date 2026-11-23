using AngleSharp.Dom;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>WCAG 2.2 checks shared by the 016 bUnit tests (2.5.3 label in name, 4.1.2 name, ARIA roles).</summary>
internal static class InstrumentViewsAccessibility
{
    /// <summary>Every button, select and input has an accessible name that contains its visible text.</summary>
    /// <param name="root">The rendered markup.</param>
    public static void EveryControlIsNamedWithItsVisibleText(IElement root)
    {
        foreach (var control in root.QuerySelectorAll("button, select, input, a[href]"))
        {
            var id = control.GetAttribute("data-testid") ?? control.OuterHtml[..Math.Min(80, control.OuterHtml.Length)];
            var visible = Visible(control);
            var name = control.GetAttribute("aria-label") ?? LabelText(root, control) ?? visible;
            name.ShouldNotBeNullOrWhiteSpace($"{id} has no accessible name");
            if (control.GetAttribute("aria-label") is { } label && visible.Length > 0)
            {
                label.ShouldContain(visible, Case.Insensitive, $"{id}: the name '{label}' must contain the visible text '{visible}' (2.5.3)");
            }
        }
    }

    // Visible text of a button or link (icon ligatures are aria-hidden); selects and inputs show their label instead.
    private static string Visible(IElement control) => control.LocalName is "button" or "a" ? Text(control).Trim() : string.Empty;

    private static string Text(INode node) => node switch
    {
        IElement e when e.GetAttribute("aria-hidden") == "true" || e.ClassList.Contains("rzi") || e.ClassList.Contains("rz-button-icon-left") => string.Empty,
        IElement e => string.Concat(e.ChildNodes.Select(Text)),
        _ => node.TextContent,
    };

    private static string? LabelText(IElement root, IElement control) =>
        (control.Closest("label") ?? ForLabel(root, control)) is { } label ? label.TextContent.Trim() : null;

    // A <label for="id"> inside the checked markup (020: labels next to selects and text areas).
    private static IElement? ForLabel(IElement root, IElement control) =>
        control.Id is { Length: > 0 } id ? root.QuerySelector($"label[for='{id}']") : null;
}
