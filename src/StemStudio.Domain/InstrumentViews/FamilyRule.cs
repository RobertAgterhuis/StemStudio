namespace StemStudio.Domain.InstrumentViews;

/// <summary>Instrument families (data-model 5.3).</summary>
public enum InstrumentFamily
{
    /// <summary>Vocals.</summary>
    Vocals,

    /// <summary>Drums.</summary>
    Drums,

    /// <summary>Bass.</summary>
    Bass,

    /// <summary>Guitar.</summary>
    Guitar,

    /// <summary>Keys (piano, keys, synth).</summary>
    Keys,

    /// <summary>Strings, brass and woodwinds.</summary>
    Orchestral,
}

/// <summary>The instrument view of a family.</summary>
public enum InstrumentView
{
    /// <summary>Singing feedback.</summary>
    Singing,

    /// <summary>Drum trainer.</summary>
    DrumTrainer,

    /// <summary>Tablature.</summary>
    Tablature,

    /// <summary>Chord chart.</summary>
    Chords,

    /// <summary>Transposed part.</summary>
    Part,
}

/// <summary>A family with its channel patterns and view (data-model 5.3).</summary>
/// <param name="Id">Family.</param>
/// <param name="Label">Label.</param>
/// <param name="Stems">Standard stems of the family.</param>
/// <param name="DetailNames">Detail names of the family (under any parent).</param>
/// <param name="View">Its instrument view.</param>
public sealed record FamilyInfo(InstrumentFamily Id, string Label, IReadOnlyList<string> Stems, IReadOnlyList<string> DetailNames, InstrumentView View);

/// <summary>
/// The family rule (data-model 5.3): a standard channel belongs to its stem's family, a detail channel to the family of
/// its detail name (parents <c>other</c>, <c>guitar</c> and <c>piano</c> may hold any instrument), a <c>rest</c> detail to
/// its parent's family.
/// </summary>
public static class FamilyRule
{
    /// <summary>The families in catalogue order.</summary>
    public static IReadOnlyList<FamilyInfo> Families { get; } =
    [
        new(InstrumentFamily.Vocals, "Vocals", ["vocals"], ["lead", "backing"], InstrumentView.Singing),
        new(InstrumentFamily.Drums, "Drums", ["drums"], ["kick", "snare", "toms", "hihat", "cymbals", "percussion"], InstrumentView.DrumTrainer),
        new(InstrumentFamily.Bass, "Bass", ["bass"], [], InstrumentView.Tablature),
        new(InstrumentFamily.Guitar, "Guitar", ["guitar"], ["acoustic-guitar", "electric-guitar"], InstrumentView.Tablature),
        new(InstrumentFamily.Keys, "Keys", ["piano"], ["keys", "synth"], InstrumentView.Chords),
        new(InstrumentFamily.Orchestral, "Strings, brass and woodwinds", ["other"], ["strings", "brass", "woodwinds"], InstrumentView.Part),
    ];

    /// <summary>The family of a channel id (<c>bass</c>, <c>other.strings</c>, <c>guitar.rest</c>).</summary>
    /// <param name="channel">The channel id.</param>
    /// <returns>The family, or null for an unknown channel.</returns>
    public static InstrumentFamily? FamilyOf(string? channel)
    {
        if (string.IsNullOrEmpty(channel))
        {
            return null;
        }

        var dot = channel.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
        {
            return OfStem(channel);
        }

        var name = channel[(dot + 1)..];
        return name == Tracks.Detail.DetailCatalog.Rest ? OfStem(channel[..dot]) : Families.FirstOrDefault(family => family.DetailNames.Contains(name, StringComparer.Ordinal))?.Id;
    }

    /// <summary>The family info of a family.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The info.</returns>
    public static FamilyInfo InfoOf(InstrumentFamily family) => Families[(int)family];

    private static InstrumentFamily? OfStem(string stem) => Families.FirstOrDefault(family => family.Stems.Contains(stem, StringComparer.Ordinal))?.Id;
}
