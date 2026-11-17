using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Agent.LyricAssist;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>023 T038/T045: syllable reports, rhymes from the real word lists (within 500 ms), language guess, the verification adapter.</summary>
public sealed class LyricLanguageEndpointTests : AssistTestBase
{
    [Fact]
    public async Task SheetSyllables_WithTheRhythmTarget()
    {
        var sheet = await SheetAsync(AssistFixtureText.Sheet("zomeravond.nl.txt"), "nl");
        (await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsAssistSettings(sheet.Id), UriKind.Relative),
            new SheetAssistSettingsUpdate { RhythmTarget = new RhythmTargetDto { Bpm = 92, BeatsPerBar = 4, BarsPerLine = 2, SyllablesPerBeat = 1.5 } }, Ct)).Dispose();
        var german = await SheetAsync("ich bin", "de");

        using var report = await Client.GetAsync(new Uri(ApiRoutes.LyricsSyllables(sheet.Id), UriKind.Relative), Ct);
        using var unsupported = await Client.GetAsync(new Uri(ApiRoutes.LyricsSyllables(german.Id), UriKind.Relative), Ct);
        using var missing = await Client.GetAsync(new Uri(ApiRoutes.LyricsSyllables(Guid.NewGuid()), UriKind.Relative), Ct);

        var body = await report.BodyAsync<SyllableReportDto>();
        body.Lines[1].ShouldSatisfyAllConditions(l => l.Syllables.ShouldBe(15), l => l.Target.ShouldBe(12), l => l.Deviates.ShouldBeTrue());
        body.TargetDurationSeconds.ShouldNotBeNull();
        ((await unsupported.JsonAsync()).GetProperty("code").GetString(), missing.StatusCode).ShouldBe(("language-unsupported", HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(report, "getSheetSyllables");
        await ContractAssert.MatchesAsync(unsupported, "getSheetSyllables");
    }

    [Fact]
    public async Task TextSyllables_ValidatesAndNormalises()
    {
        using var ok = await PostAsync(ApiRoutes.LyricAssistSyllables, new SyllableRequest { Language = AssistLanguageDto.En, Text = "[00:01.00]The radio is playing\r\n[chorus]" });
        using var empty = await PostAsync(ApiRoutes.LyricAssistSyllables, new SyllableRequest { Language = AssistLanguageDto.En, Text = "" });
        using var badTarget = await PostAsync(ApiRoutes.LyricAssistSyllables, new SyllableRequest { Language = AssistLanguageDto.En, Text = "a", RhythmTarget = new RhythmTargetDto { Bpm = 1, BeatsPerBar = 4, BarsPerLine = 1, SyllablesPerBeat = 1 } });

        (await ok.BodyAsync<SyllableReportDto>()).Lines.Select(l => (l.Syllables, l.IsMarker)).ShouldBe([(7, false), (0, true)]);
        (await empty.FieldErrorsAsync("text")).ShouldNotBeEmpty();
        (await badTarget.FieldErrorsAsync("rhythmTarget.bpm")).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(ok, "countSyllables");
        await ContractAssert.MatchesAsync(empty, "countSyllables");
    }

    [Theory]
    [InlineData("nacht", "nl", "kracht")]
    [InlineData("night", "en", "light")]
    public async Task Rhymes_FromLocalDataWithin500Ms(string word, string language, string expected)
    {
        using (await Client.GetAsync(new Uri($"{ApiRoutes.LyricAssistRhymes}?word=warm&language={language}", UriKind.Relative), Ct))
        {
            // Warms the lazy index (built once per language).
        }

        var watch = Stopwatch.StartNew();
        using var response = await Client.GetAsync(new Uri($"{ApiRoutes.LyricAssistRhymes}?word={word}&language={language}&limit=60", UriKind.Relative), Ct);
        watch.Stop();

        var result = await response.BodyAsync<RhymeResultDto>();
        result.Rhymes.ShouldContain(r => r.Word == expected && r.Kind == RhymeKindDto.Perfect);
        result.Rhymes.Count.ShouldBeLessThanOrEqualTo(60);
        watch.ElapsedMilliseconds.ShouldBeLessThan(500);
        await ContractAssert.MatchesAsync(response, "findRhymes");
    }

    [Theory]
    [InlineData("?language=nl", "word")]
    [InlineData("?word=two%20words&language=nl", "word")]
    [InlineData("?word=nacht", "language")]
    [InlineData("?word=nacht&language=de", "language")]
    [InlineData("?word=nacht&language=nl&limit=0", "limit")]
    [InlineData("?word=nacht&language=nl&limit=x", "limit")]
    public async Task Rhymes_InvalidQueriesAre400(string query, string field)
    {
        using var response = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistRhymes + query, UriKind.Relative), Ct);

        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(response, "findRhymes");
    }

    [Fact]
    public async Task LanguageGuess()
    {
        using var english = await PostAsync(ApiRoutes.LyricAssistLanguageGuess, new LanguageGuessRequest { Text = "the cat sat on the mat" });
        using var none = await PostAsync(ApiRoutes.LyricAssistLanguageGuess, new LanguageGuessRequest { Text = "qqq zzz" });
        using var empty = await PostAsync(ApiRoutes.LyricAssistLanguageGuess, new LanguageGuessRequest { Text = "" });

        (await english.BodyAsync<LanguageGuessDto>()).ShouldSatisfyAllConditions(g => g.Language.ShouldBe(AssistLanguageDto.En), g => g.Confidence.ShouldBe(1));
        (await none.BodyAsync<LanguageGuessDto>()).Language.ShouldBeNull();
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.MatchesAsync(english, "guessLyricsLanguage");
        await ContractAssert.MatchesAsync(none, "guessLyricsLanguage");
    }

    [Fact]
    public void Verification_CountsRhymesAndWithholds()
    {
        var verification = Factory.Services.GetRequiredService<ILyricVerification>();

        verification.CountSyllables("The radio is playing", AssistLanguageDto.En).ShouldBe(7);
        verification.CountSyllables("[verse]", AssistLanguageDto.Nl).ShouldBe(0);
        (verification.Rhyme("night", "light", AssistLanguageDto.En), verification.Rhyme("home", "alone", AssistLanguageDto.En), verification.Rhyme("night", "rain", AssistLanguageDto.En))
            .ShouldBe((RhymeKindDto.Perfect, RhymeKindDto.Near, null));
        verification.IsVerbatim("nothing from the library here at all tonight").ShouldBeFalse();
        Factory.Services.GetRequiredService<ILyricAssistantFactory>().ShouldBeOfType<FakeAssistantFactory>();
    }

    [Fact]
    public void RealFactory_IsRegisteredByTheAgentSeam()
    {
        using var host = new ApiFactory();

        host.Services.GetRequiredService<ILyricAssistantFactory>().ShouldBeOfType<LyricAssistantFactory>();
        host.Services.GetRequiredService<IWordList>().Words(AssistLanguage.Dutch).ShouldContain("nacht");
    }

    private Task<HttpResponseMessage> PostAsync<T>(string route, T body) => Client.PostAsJsonAsync(new Uri(route, UriKind.Relative), body, Ct);
}
