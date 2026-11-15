namespace StemStudio.Web.Components.Design;

/// <summary>Red stub (T025).</summary>
public static class StemColor
{
    /// <summary>Stub.</summary>
    /// <param name="stem">Stem.</param>
    /// <returns>Value.</returns>
    public static string Var(string stem) => stem;

    /// <summary>Stub.</summary>
    /// <param name="index">Index.</param>
    /// <returns>Value.</returns>
    public static string Pattern(int index) => index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Stub.</summary>
    /// <param name="stem">Stem.</param>
    /// <returns>Value.</returns>
    public static string Parent(string stem) => stem;
}

/// <summary>Red stub (T025).</summary>
public enum SplitState
{
    /// <summary>Idle.</summary>
    Idle,

    /// <summary>Running.</summary>
    Running,

    /// <summary>Done.</summary>
    Done,
}
