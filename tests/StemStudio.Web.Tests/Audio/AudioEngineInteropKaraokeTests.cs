using System.Text.Json;
using Bunit;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Tests.Audio;

/// <summary>Engine contract 2.1.0 karaoke methods (feature 006): attachKaraoke, setKaraokeOffset, detachKaraoke.</summary>
public sealed partial class AudioEngineInteropTests
{
    private static readonly KaraokeLyrics Lyrics = new(
    [
        new KaraokeLine(10950, 13200, [new KaraokeWord("Fake", 10950, 11400), new KaraokeWord("line", 11400, 13200)]),
    ]);

    [Fact]
    public async Task AttachKaraoke_SendsHostLyricsAndOptions_InTheContractShape()
    {
        await _interop.InitializeAsync();

        await _interop.AttachKaraokeAsync("karaoke-host", Lyrics, new KaraokeOptions(250));

        var args = _engine.VerifyInvoke("attachKaraoke").Arguments;
        args[0].ShouldBe("karaoke-host");
        using var lyrics = JsonDocument.Parse(JsonSerializer.Serialize(args[1], JsonSerializerOptions.Web));
        var word = lyrics.RootElement.GetProperty("lines")[0].GetProperty("words")[1];
        (word.GetProperty("text").GetString(), word.GetProperty("startMs").GetInt32(), word.GetProperty("endMs").GetInt32()).ShouldBe(("line", 11400, 13200));
        lyrics.RootElement.GetProperty("lines")[0].GetProperty("startMs").GetInt32().ShouldBe(10950);
        JsonSerializer.Serialize(args[2], JsonSerializerOptions.Web).ShouldBe("{\"offsetMs\":250}");
    }

    [Fact]
    public async Task AttachKaraoke_PassesReducedMotion_OnlyWhenTheAppSetsIt()
    {
        await _interop.InitializeAsync();

        await _interop.AttachKaraokeAsync("karaoke-host", Lyrics, new KaraokeOptions(0, true));

        JsonSerializer.Serialize(_engine.VerifyInvoke("attachKaraoke").Arguments[2], JsonSerializerOptions.Web)
            .ShouldBe("{\"offsetMs\":0,\"reducedMotion\":true}");
    }

    [Fact]
    public async Task SetKaraokeOffset_AndDetach_InvokeTheContractMethods()
    {
        await _interop.InitializeAsync();

        await _interop.SetKaraokeOffsetAsync(-120);
        await _interop.DetachKaraokeAsync();

        _engine.VerifyInvoke("setKaraokeOffset").Arguments.ShouldBe([-120]);
        _engine.VerifyInvoke("detachKaraoke").Arguments.ShouldBeEmpty();
    }

    [Fact]
    public async Task DetachKaraoke_WithoutAnEngine_IsANoOp()
    {
        await _interop.DetachKaraokeAsync();

        _engine.Invocations.ShouldBeEmpty();
    }

    [Fact]
    public async Task KaraokeErrors_SurfaceAsAudioEngineException()
    {
        _engine.SetupVoid("attachKaraoke", _ => true).SetException(new Microsoft.JSInterop.JSException("Karaoke host not found: karaoke-host"));
        await _interop.InitializeAsync();

        var error = await Should.ThrowAsync<AudioEngineException>(() => _interop.AttachKaraokeAsync("karaoke-host", Lyrics, new KaraokeOptions(0)));

        error.Message.ShouldContain("Karaoke host not found");
    }

    [Fact]
    public void KaraokeRecords_NeverPrintTheText()
    {
        Lyrics.ToString().ShouldNotContain("Fake");
        Lyrics.Lines[0].ToString().ShouldNotContain("line");
        Lyrics.Lines[0].Words[0].ToString().ShouldNotContain("Fake");
        Lyrics.Lines[0].Words[0].ToString().ShouldContain("10950");
    }

    [Fact]
    public void EngineMethods_ListTheKaraokeMethods() =>
        EngineMethods.All.ShouldContain("attachKaraoke");
}
