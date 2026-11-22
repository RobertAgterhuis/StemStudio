namespace StemStudio.Web.InstrumentViews;

/// <summary>Capo mode of one track (016 US8, data-model 2.9 and 8): on/off, the capo 0..12 and the sounding symbols (Advanced).</summary>
/// <param name="On">Capo mode is on.</param>
/// <param name="Capo">The capo 0..12.</param>
/// <param name="ShowSounding">Show the sounding symbol next to each shape.</param>
public sealed record CapoSetting(bool On, int Capo, bool ShowSounding)
{
    /// <summary>Capo mode off.</summary>
    public static CapoSetting Off { get; } = new(false, 0, false);

    /// <summary>The capo the chord views use: the capo while on, else 0.</summary>
    public int Effective => On ? Capo : 0;
}
