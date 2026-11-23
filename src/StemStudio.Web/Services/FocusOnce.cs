using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace StemStudio.Web.Services;

/// <summary>Moves the focus to an element; a gone element or a closed circuit leaves the focus where it is.</summary>
public static class FocusOnce
{
    /// <summary>Focuses the element when it still exists.</summary>
    /// <param name="element">The element.</param>
    /// <returns>A task.</returns>
    public static async Task TryAsync(ElementReference element)
    {
        try
        {
            await element.FocusAsync();
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // The element is gone or the circuit closed; focus stays where it is.
        }
    }
}
