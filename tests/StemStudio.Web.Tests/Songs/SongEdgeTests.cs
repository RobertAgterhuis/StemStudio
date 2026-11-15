using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Songs;
using StemStudio.Web.Generation;
using StemStudio.Web.Songs;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Songs;

/// <summary>010 polish (T058): the remaining rules of the song form, titles, texts and loading states.</summary>
public sealed class SongEdgeTests : PageTestContext
{
    [Fact]
    public void SongForm_WithoutStyles_StartsWithAFreeDescription()
    {
        var form = new SongForm([], []);

        form.StyleId.ShouldBeNull();
        form.FreeCaption = "  warm piano  ";
        form.Text = SongData.Lyrics;
        form.Request().FreeCaption.ShouldBe("warm piano");
    }

    [Fact]
    public void SongForm_InputsFallBack_AndAreChecked()
    {
        var form = new SongForm(GenerationData.Styles(), []) { Text = SongData.Lyrics };

        form.SetDuration(600);
        form.Duration.ShouldBe(480);
        form.SetDuration(null);
        form.Duration.ShouldBe(480);
        form.SetTakes(2);
        form.SetTakes(null);
        form.Takes.ShouldBe(2);
        form.SetBpm(250);
        form.Validate().ShouldBe(new FieldProblem("bpm", "The tempo must be 40 to 220 BPM."));
        form.SetBpm(null);
        form.SetTakes(3);
        form.Validate().ShouldBe(new FieldProblem("takes", "Choose 1 or 2 takes."));
    }

    [Fact]
    public void SongForm_ChoosingNewLyrics_KeepsTheText()
    {
        var form = new SongForm(GenerationData.Styles(), [SongData.Sheet()]);
        form.SelectSheet(SongData.SheetId);

        form.SelectSheet(null);

        (form.Sheet, form.Text).ShouldBe((null, SongData.Lyrics));
        form.LyricsInput().Title.ShouldBe("Paper boats");
    }

    [Fact]
    public void Texts_CoverEveryCase()
    {
        SongText.TimeSignature("5").ShouldBe("5");
        SongText.Words("[verse 2]\nOne two  three\n[chorus]").ShouldBe(3);
        LanguageNames.Of("xx").ShouldBe("xx");
        GenerationTitles.Of(GenerationData.Generation() with { Restyle = null }).ShouldBe("Generation");
        GenerationStatusText.VoiceReason(VoiceData.VoiceStatus(GenerationAvailabilityDto.Unavailable)).ShouldBe("The voice features are not available on this computer.");
    }

    [Fact]
    public void SongFacts_WithoutStyleOrMusicChoices_SayTheModelChooses()
    {
        var details = SongData.Details() with { Style = null, FreeCaption = null, Bpm = null, KeyScale = null, TimeSignature = null, Voice = new SongVoiceDto { LeadVoice = LeadVoiceDto.Any } };
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns([SongData.Song() with { Song = details }]);

        var page = Render<StemStudio.Web.Components.Generation.GenerationList>(p => p.Add(c => c.Query, new GenerationQuery()));

        var text = page.Find($"[data-testid=take-details-{SongData.SongId}-1]").TextContent;
        text.ShouldContain("StyleChosen by the model");
        text.ShouldContain("MusicChosen by the model");
        text.ShouldContain("Any lead");
    }

    [Fact]
    public void SongFacts_ShowAFreeDescription_AndAnInstrumentalSong()
    {
        var details = SongData.Details(instrumental: true) with { Style = null, FreeCaption = "warm acoustic guitar" };
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns([SongData.Song(instrumental: true) with { Song = details }]);

        var page = Render<StemStudio.Web.Components.Generation.GenerationList>(p => p.Add(c => c.Query, new GenerationQuery()));

        var text = page.Find($"[data-testid=take-details-{SongData.SongId}-1]").TextContent;
        text.ShouldContain("warm acoustic guitar");
        text.ShouldContain("Instrumental (no vocals)");
    }

    [Fact]
    public void SongPage_LoadingAndLoadFailure()
    {
        Api.ListStylesAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<StyleDto>>().Task);
        Render<NewSongPage>().FindAll(".ss-skeleton").ShouldNotBeEmpty();

        Api.ListStylesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));
        Render<NewSongPage>().Find("[data-testid=song-load-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");
    }

    [Fact]
    public async Task SongPage_BackToNewLyrics_AndAFailedGenerate()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.Status());
        Api.ListLyricsSheetsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([SongData.Sheet()]);
        Api.CreateSongAsync(Arg.Any<SongRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.Conflict, "GPU required", "No NVIDIA GPU is available to the generator."));
        var page = Render<NewSongPage>();

        await page.Find("[data-testid=song-lyrics-select]").ChangeAsync(new() { Value = SongData.SheetId.ToString() });
        await page.Find("[data-testid=song-lyrics-select]").ChangeAsync(new() { Value = string.Empty });
        await page.Find("[data-testid=song-generate]").ClickAsync(new());

        page.Find("[data-testid=song-error]").TextContent.ShouldBe("No NVIDIA GPU is available to the generator.");
        page.Find("[data-testid=song-error]").HasAttribute("data-field").ShouldBeFalse();
    }

    [Fact]
    public async Task LyricsDialog_EscapeCloses_AndTheListShowsItsSkeletonWhileLoading()
    {
        var page = Render<LyricsListPage>();
        await page.Find("[data-testid=lyrics-new]").ClickAsync(new());

        await page.Find("[role=dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        page.FindAll("[role=dialog]").ShouldBeEmpty();
        Api.ListLyricsSheetsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<LyricsSheetDto>>().Task);
        Render<LyricsListPage>().FindAll(".ss-skeleton").ShouldNotBeEmpty();
    }
}
