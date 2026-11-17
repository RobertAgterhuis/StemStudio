using System.Text.Json;
using StemStudio.Domain.Lyrics;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>Reads the 023 fixtures in <c>tests/fixtures/lyrics/assist/</c> (architect T009) from the repository.</summary>
internal static class AssistFixtures
{
    public static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    public static string Root { get; } = Path.Combine(FindRoot(), "tests", "fixtures", "lyrics", "assist");

    public static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative)).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>A normalised sheet text from <c>sheets/</c>.</summary>
    public static string Sheet(string file) => LyricsNormaliser.Normalize(Read(Path.Combine("sheets", file)));

    public static JsonElement Expected() => JsonDocument.Parse(Read("expected.json")).RootElement;

    public static LyricsSheet NewSheet(string text = "line one\nline two", string language = "en", string title = "Song") =>
        LyricsSheet.Create(Guid.NewGuid(), new LyricsContent(title, language, text), Now);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "StemStudio.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("StemStudio.slnx not found above the test output.");
    }
}
