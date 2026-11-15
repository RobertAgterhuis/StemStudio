namespace StemStudio.Web.Services;

/// <summary>
/// Which modal overlay of the shell is open: the command palette or the shortcut overlay (FR-017, FR-018).
/// Components anywhere (context bar, help panel, shortcut handler) open them; the shell renders them. Scoped.
/// </summary>
public sealed class ShellOverlays
{
    /// <summary>Raised when an overlay opens or closes.</summary>
    public event Action? Changed;

    /// <summary>True while the command palette is open.</summary>
    public bool PaletteOpen { get; private set; }

    /// <summary>True while the shortcut overlay is open.</summary>
    public bool ShortcutsOpen { get; private set; }

    /// <summary>Opens the command palette (closes the shortcut overlay).</summary>
    public void OpenPalette() => Set(palette: true, shortcuts: false);

    /// <summary>Opens the shortcut overlay (closes the palette).</summary>
    public void OpenShortcuts() => Set(palette: false, shortcuts: true);

    /// <summary>Closes both overlays.</summary>
    public void Close() => Set(palette: false, shortcuts: false);

    private void Set(bool palette, bool shortcuts)
    {
        PaletteOpen = palette;
        ShortcutsOpen = shortcuts;
        Changed?.Invoke();
    }
}
