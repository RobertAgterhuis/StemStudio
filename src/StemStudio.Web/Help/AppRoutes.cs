using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace StemStudio.Web.Help;

/// <summary>Matches the current location against route templates such as <c>/tracks/{id}/mixer</c>.</summary>
public static class AppRoutes
{
    /// <summary>The app-relative path of the current location without query and fragment, e.g. <c>/tracks/x</c>.</summary>
    /// <param name="navigation">The navigation manager.</param>
    /// <returns>The path, starting with <c>/</c>.</returns>
    public static string CurrentPath(NavigationManager navigation) =>
        "/" + navigation.ToBaseRelativePath(navigation.Uri).Split('?', '#')[0].TrimEnd('/');

    /// <summary>True when a path matches a route template; <c>*</c> matches every path.</summary>
    /// <param name="template">The route template.</param>
    /// <param name="path">The path, starting with <c>/</c>.</param>
    /// <returns>Whether the template matches.</returns>
    public static bool Matches(string template, string path)
    {
        if (template == "*")
        {
            return true;
        }

        var pattern = "^" + Regex.Replace(Regex.Escape(template.TrimEnd('/')), @"\\\{[^}]+}", "[^/]+") + "/?$";
        return Regex.IsMatch(path, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}
