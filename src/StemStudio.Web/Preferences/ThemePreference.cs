namespace StemStudio.Web.Preferences;

/// <summary>Stored theme choice (data-model.md 1.1 <c>theme</c>).</summary>
public enum ThemePreference
{
    /// <summary>Follow the OS colour scheme (<c>prefers-color-scheme</c>). Default.</summary>
    System = 0,

    /// <summary>Always the light theme.</summary>
    Light = 1,

    /// <summary>Always the dark theme.</summary>
    Dark = 2,
}
