namespace StemStudio.Web.Services;

/// <summary>
/// Open state and roving focus of a WAI-ARIA menu button (APG): ArrowDown on the button opens at the first item and
/// ArrowUp at the last; in the menu the arrows, Home and End move (wrapping), Escape closes and returns the focus to the
/// button, Tab closes and lets the focus move on. <see cref="FocusPending"/> tells the component to move the focus
/// after the next render.
/// </summary>
public sealed class MenuButtonState
{
    /// <summary>Whether the menu is open.</summary>
    public bool Open { get; private set; }

    /// <summary>The item with <c>tabindex=0</c>.</summary>
    public int Focus { get; private set; }

    /// <summary>The focus must move (to <see cref="Focus"/> when open, else to the button); reading it clears it.</summary>
    public bool TakeFocusPending()
    {
        var pending = FocusPending;
        FocusPending = false;
        return pending;
    }

    private bool FocusPending { get; set; }

    /// <summary>Click on the button: opens at the first item or closes.</summary>
    public void Toggle()
    {
        if (Open)
        {
            Close();
        }
        else
        {
            OpenAt(0);
        }
    }

    /// <summary>A key on the closed button.</summary>
    /// <param name="key">The key.</param>
    /// <param name="count">Number of items.</param>
    public void ButtonKey(string key, int count)
    {
        if (!Open && key is "ArrowDown" or "ArrowUp")
        {
            OpenAt(key == "ArrowUp" ? count - 1 : 0);
        }
    }

    /// <summary>A key inside the open menu.</summary>
    /// <param name="key">The key.</param>
    /// <param name="count">Number of items.</param>
    public void MenuKey(string key, int count)
    {
        if (key is "Escape" or "Tab")
        {
            (Open, FocusPending) = (false, key == "Escape");
        }
        else if (MenuKeys.Move(key, Focus, count) is { } next)
        {
            (Focus, FocusPending) = (next, true);
        }
    }

    /// <summary>Closes the menu and returns the focus to the button (an item was chosen).</summary>
    public void Close() => (Open, FocusPending) = (false, true);

    private void OpenAt(int index) => (Open, Focus, FocusPending) = (true, index, true);
}
