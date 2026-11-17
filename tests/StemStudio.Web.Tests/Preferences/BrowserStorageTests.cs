using Microsoft.JSInterop;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Web.Preferences;

namespace StemStudio.Web.Tests.Preferences;

/// <summary>Best-effort local storage of small UI choices (karaoke banner, practice panel): unavailable storage never breaks the UI.</summary>
public sealed class BrowserStorageTests
{
    private readonly IJSRuntime _js = Substitute.For<IJSRuntime>();

    public static TheoryData<Exception> Unavailable() =>
    [
        new JSException("SecurityError: storage is disabled"),
        new JSDisconnectedException("circuit gone"),
        new InvalidOperationException("JavaScript interop calls cannot be issued at this time"),
    ];

    [Fact]
    public async Task Get_ReturnsTheStoredText()
    {
        _js.InvokeAsync<string?>("localStorage.getItem", Arg.Any<object?[]?>()).Returns("{\"expanded\":true}");

        (await BrowserStorage.GetAsync(_js, "k")).ShouldBe("{\"expanded\":true}");
    }

    [Theory]
    [MemberData(nameof(Unavailable))]
    public async Task Get_GivesNull_WhenStorageIsUnavailable(Exception error)
    {
        _js.InvokeAsync<string?>("localStorage.getItem", Arg.Any<object?[]?>()).ThrowsAsync(error);

        (await BrowserStorage.GetAsync(_js, "k")).ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Unavailable))]
    public async Task Set_IsSkipped_WhenStorageIsUnavailable(Exception error)
    {
        _js.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("localStorage.setItem", Arg.Any<object?[]?>()).ThrowsAsync(error);

        await Should.NotThrowAsync(() => BrowserStorage.SetAsync(_js, "k", "v"));
    }

    [Fact]
    public async Task OtherErrors_AreNotSwallowed()
    {
        _js.InvokeAsync<string?>("localStorage.getItem", Arg.Any<object?[]?>()).ThrowsAsync(new TaskCanceledException());

        await Should.ThrowAsync<TaskCanceledException>(() => BrowserStorage.GetAsync(_js, "k"));
    }
}
