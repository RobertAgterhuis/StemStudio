using StemStudio.Domain.Styles;

namespace StemStudio.Domain.Tests.Styles;

internal static class StyleTestData
{
    public static readonly DateTime T0 = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    public static StyleFields RainyCafe() => new()
    {
        Name = "Rainy cafe jazz",
        GenreFamily = GenreFamily.Jazz,
        SubGenre = "cool jazz",
        TempoFeel = TempoFeel.Laidback,
        Instrumentation = ["muted trumpet", "piano", "upright bass"],
        Moods = ["melancholic", "intimate"],
        VocalHandling = VocalHandling.Instrumental,
        DefaultStrength = Strength.Light,
        Description = "Late-night trio with a soft muted trumpet.",
    };

    public static StyleFields JazzTrio() => new()
    {
        Name = "Jazz trio",
        GenreFamily = GenreFamily.Jazz,
        SubGenre = "swing",
        TempoFeel = TempoFeel.Swing,
        Instrumentation = ["piano", "upright bass", "brushed drums"],
        Moods = ["relaxed", "warm"],
        VocalHandling = VocalHandling.Keep,
        DefaultStrength = Strength.Balanced,
        Description = "A small acoustic jazz combo with a swinging ride and walking bass.",
    };

    public static Style User(StyleFields? fields = null) => Style.CreateUser(Guid.NewGuid(), fields ?? RainyCafe(), T0);

    public static Style Preset() => Style.FromPreset(new PresetDefinition(Guid.Parse("3f9c2a1e-6b4d-4e8f-a0c2-1d3e5f7a9b01"), "jazz-trio", JazzTrio()), T0);
}
