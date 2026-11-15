namespace StemStudio.Web.Tests.Help;

/// <summary>Help panel state (T048, FR-023): context resolution, history, navigation, focus return.</summary>
public sealed class HelpContextTests : HelpTestContext
{
    [Fact]
    public async Task OpenWithoutTopic_UsesTheFocusedMarkersTopic()
    {
        Interop.FocusedHelpId = "mixer-pan";

        await Help.OpenAsync();

        Help.IsOpen.ShouldBeTrue();
        Help.TopicId.ShouldBe("mixer-pan");
        Interop.FocusCaptures.ShouldBe(1);
    }

    [Fact]
    public async Task OpenWithoutTopic_FallsBackToTheScreenTopic()
    {
        Interop.FocusedHelpId = "no-topic-for-this";

        await Help.OpenAsync();

        Help.TopicId.ShouldBe("import-screen");
    }

    [Fact]
    public async Task ScreenTopic_FollowsTheRouteTemplate()
    {
        GoTo($"tracks/{Fakes.TestData.TrackId}/mixer?mixId=1");

        await Help.OpenScreenAsync();

        Help.TopicId.ShouldBe("mixer-screen");
    }

    [Fact]
    public async Task ScreenWithoutTopic_ShowsTheIndex()
    {
        GoTo("mixes");

        await Help.OpenScreenAsync();

        Help.IsOpen.ShouldBeTrue();
        Help.TopicId.ShouldBeNull();
    }

    [Fact]
    public async Task UnknownTopic_ShowsTheIndex()
    {
        await Help.OpenAsync("nope");

        Help.TopicId.ShouldBeNull();
    }

    [Fact]
    public async Task Show_PushesHistory_AndBackReturns()
    {
        await Help.OpenAsync("mixer-pan");
        await Help.ShowAsync("stem");

        Help.CanGoBack.ShouldBeTrue();
        Help.Back();

        Help.TopicId.ShouldBe("mixer-pan");
        Help.CanGoBack.ShouldBeFalse();
        Help.Back();
        Help.TopicId.ShouldBe("mixer-pan");
    }

    [Fact]
    public async Task ShowWhileClosed_Opens()
    {
        await Help.ShowAsync("stem");

        Help.IsOpen.ShouldBeTrue();
        Help.CanGoBack.ShouldBeFalse();
    }

    [Fact]
    public async Task Close_RestoresFocus_AndReopeningCapturesAgain()
    {
        await Help.OpenAsync("stem");

        await Help.CloseAsync();
        await Help.CloseAsync();

        Help.IsOpen.ShouldBeFalse();
        Interop.FocusRestores.ShouldBe(1);
        await Help.OpenAsync("stem");
        Interop.FocusCaptures.ShouldBe(2);
    }

    [Fact]
    public async Task Toggle_OpensTheScreenTopic_ThenCloses()
    {
        Interop.FocusedHelpId = "mixer-pan";

        await Help.ToggleAsync();
        Help.TopicId.ShouldBe("import-screen");

        await Help.ToggleAsync();
        Help.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenPanel_FollowsNavigation_ToTheNewScreenTopic()
    {
        await Help.OpenAsync("mixer-pan");
        await Help.ShowAsync("stem");
        var changes = 0;
        Help.Changed += () => changes++;

        GoTo("tracks");

        Help.TopicId.ShouldBe("tracks-library");
        Help.CanGoBack.ShouldBeFalse();
        changes.ShouldBe(1);
    }

    [Fact]
    public void ClosedPanel_IgnoresNavigation()
    {
        var changes = 0;
        Help.Changed += () => changes++;

        GoTo("tracks");

        changes.ShouldBe(0);
    }

    [Fact]
    public async Task Open_RaisesChanged()
    {
        var changes = 0;
        Help.Changed += () => changes++;

        await Help.OpenAsync("stem");

        changes.ShouldBe(1);
    }
}
