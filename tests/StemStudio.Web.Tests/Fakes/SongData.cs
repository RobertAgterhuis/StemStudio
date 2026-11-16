using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>Contract-shaped lyrics sheets and song generations (backend-api 1.9.0 examples, feature 010). Self-written lyrics only.</summary>
public static class SongData
{
    public static readonly Guid SheetId = Guid.Parse("3e8d0c9b-7a6f-4e5d-8c4b-a3f2e1d0c9b8");
    public static readonly Guid SongId = Guid.Parse("4f9e1d0c-8b7a-4f6e-9d5c-b4a3f2e1d0c9");

    public const string Lyrics = "Paper boats along the gutter\nRacing rain down to the sea\n\nHold the line and hold the colour\nSing it back to me";

    public static LyricsSheetDto Sheet(string title = "Paper boats", string text = Lyrics) => new()
    {
        Id = SheetId,
        Title = title,
        Language = "en",
        Text = text,
        CreatedAt = TestData.Created,
        UpdatedAt = TestData.Created,
    };

    public static SongDetailsDto Details(bool instrumental = false) => new()
    {
        Lyrics = new LyricsSnapshotDto { LyricsId = SheetId, Title = "Paper boats", Language = "en", Text = Lyrics },
        Style = GenerationData.Snapshot(GenerationData.Jazz()),
        Voice = new SongVoiceDto { LeadVoice = LeadVoiceDto.Male, Delivery = "breathy, soft" },
        Instrumental = instrumental,
        Bpm = 96,
        KeyScale = "E minor",
        TimeSignature = "4",
        DurationSeconds = 30,
        Caption = "jazz, small combo, male vocal, breathy, soft",
    };

    public static GenerationDto Song(GenerationStateDto status = GenerationStateDto.Completed, bool instrumental = false) =>
        GenerationData.Generation(status, SongId) with
        {
            Kind = GenerationKindDto.Song,
            Restyle = null,
            Song = Details(instrumental),
            Takes = status == GenerationStateDto.Completed
                ? [GenerationData.Take(1, SongId) with { DurationSeconds = 30, Vocal = instrumental ? VocalLabelDto.None : VocalLabelDto.Synthetic }]
                : [],
        };

    public static SectionProposalDto Proposal() => new()
    {
        Text = "[verse]\nPaper boats along the gutter\nRacing rain down to the sea\n\n[chorus]\nHold the line and hold the colour\nSing it back to me",
        Sections = [new LyricsSectionDto { Marker = SectionMarkerDto.Verse, LineCount = 2 }, new LyricsSectionDto { Marker = SectionMarkerDto.Chorus, LineCount = 2 }],
        WordsPerSecondAt120s = 0.17,
    };

    public static string Words(int count) => string.Join(' ', Enumerable.Repeat("la", count));

    public static string TooLong() => new('a', ContractLimits.LyricsSheetMaxLength + 1);
}
