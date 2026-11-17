namespace StemStudio.Domain.Styles;

/// <summary>Style name rules: case-insensitive uniqueness and copy names.</summary>
public static class StyleNaming
{
    /// <summary>The upper-case trimmed name used by the unique index.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The normalised name.</returns>
    public static string Normalize(string name) => CopyNames.Normalize(name);

    /// <summary>"&lt;name&gt; (copy)", numbered when taken, at most 60 characters.</summary>
    /// <param name="name">The name to copy.</param>
    /// <param name="takenNames">Names in the library.</param>
    /// <returns>A free name.</returns>
    public static string CopyName(string name, IEnumerable<string> takenNames) => CopyNames.Next(name, takenNames, StyleRules.MaxNameLength);

    /// <summary>"&lt;name&gt; (restored)", numbered when taken, at most 60 characters (013 merge, research R-07).</summary>
    /// <param name="name">The archived name.</param>
    /// <param name="takenNames">Names in the library.</param>
    /// <returns>A free name.</returns>
    public static string RestoredName(string name, IEnumerable<string> takenNames) => CopyNames.Next(name, takenNames, StyleRules.MaxNameLength, CopyNames.RestoredWord);
}
