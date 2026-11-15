namespace StemStudio.Web.Preferences;

/// <summary>Stored density choice (data-model.md 1.1 <c>density</c>).</summary>
public enum DensityPreference
{
    /// <summary>Compact at Expert, comfortable otherwise. Default.</summary>
    Auto = 0,

    /// <summary>Comfortable spacing.</summary>
    Comfortable = 1,

    /// <summary>Compact spacing.</summary>
    Compact = 2,
}
