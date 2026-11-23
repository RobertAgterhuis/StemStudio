using StemStudio.Web.Services;

namespace StemStudio.Web.Tests.Handouts;

/// <summary>Menu keys of the WAI-ARIA menu button (APG): wrapping arrows, Home, End; other keys do not move.</summary>
public sealed class MenuKeysTests
{
    [Theory]
    [InlineData("ArrowDown", 7, 0)]
    [InlineData("ArrowUp", 0, 7)]
    [InlineData("Home", 4, 0)]
    [InlineData("End", 1, 7)]
    public void Keys_MoveAndWrap(string key, int current, int expected) => MenuKeys.Move(key, current, 8).ShouldBe(expected);

    [Fact]
    public void OtherKeys_DoNotMove() => MenuKeys.Move("a", 3, 8).ShouldBeNull();

    [Fact]
    public void State_TabClosesWithoutReturningTheFocus_EscapeReturnsIt()
    {
        var menu = new MenuButtonState();
        menu.Toggle();
        menu.TakeFocusPending().ShouldBeTrue();

        menu.MenuKey("Tab", 8);
        (menu.Open, menu.TakeFocusPending()).ShouldBe((false, false));
        menu.ButtonKey("ArrowUp", 8);
        menu.Focus.ShouldBe(7);
        menu.MenuKey("Escape", 8);
        (menu.Open, menu.TakeFocusPending()).ShouldBe((false, true));
    }
}
