using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>The "Practise as" menu button (WAI-ARIA APG menu button pattern with radio items).</summary>
public sealed partial class PractiseAsMenu
{
    private readonly ElementReference[] _items = new ElementReference[InstrumentFamilies.All.Count];
    private ElementReference _button;
    private int _focus;
    private bool _focusPending;

    /// <summary>The family practised now, or null.</summary>
    [Parameter]
    public InstrumentFamilyDto? Family { get; set; }

    /// <summary>Raised with the chosen family.</summary>
    [Parameter]
    public EventCallback<InstrumentFamilyDto> OnChoose { get; set; }

    private bool Open { get; set; }

    private string FamilyId => Family is { } family ? InstrumentFamilies.Id(family) : string.Empty;

    private string ButtonText => Family is { } family ? $"Practise as: {InstrumentFamilies.Label(family)}" : "Practise as ...";

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusPending)
        {
            _focusPending = false;
            await FocusAsync(Open ? _items[_focus] : _button);
        }
    }

    private void Toggle()
    {
        if (Open)
        {
            Close();
            return;
        }

        OpenAt(Family is { } family ? IndexOf(family) : 0);
    }

    private void ButtonKey(KeyboardEventArgs args)
    {
        if (!Open && args.Key is "ArrowDown" or "ArrowUp")
        {
            OpenAt(args.Key == "ArrowUp" ? InstrumentFamilies.All.Count - 1 : 0);
        }
    }

    private void MenuKey(KeyboardEventArgs args)
    {
        var count = InstrumentFamilies.All.Count;
        switch (args.Key)
        {
            case "Escape":
                Close();
                return;
            case "ArrowDown" or "ArrowUp" or "Home" or "End":
                MoveTo(args.Key, count);
                return;
        }
    }

    private void MoveTo(string key, int count)
    {
        _focus = key switch
        {
            "ArrowDown" => (_focus + 1) % count,
            "ArrowUp" => (_focus + count - 1) % count,
            "Home" => 0,
            _ => count - 1,
        };
        _focusPending = true;
    }

    private void OpenAt(int index) => (Open, _focus, _focusPending) = (true, index, true);

    private void Close() => (Open, _focusPending) = (false, true);

    private Task ChooseAsync(InstrumentFamilyDto family)
    {
        Close();
        return OnChoose.InvokeAsync(family);
    }

    private static int IndexOf(InstrumentFamilyDto family) => Math.Max(0, InstrumentFamilies.All.ToList().IndexOf(family));

    private static async Task FocusAsync(ElementReference element)
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
