using Bunit;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Experience;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>The transpose spinbutton, its buttons and the Expert number entry (012 T037, FR-002, FR-017).</summary>
public sealed class TransposeControlTests : PracticeTestContext
{
    [Fact]
    public void Spinbutton_HasTheContractRangeAndSpokenValue()
    {
        var page = RenderAt();

        var spin = page.Find("[data-testid=practice-transpose]");
        spin.GetAttribute("role").ShouldBe("spinbutton");
        spin.GetAttribute("tabindex").ShouldBe("0");
        (spin.GetAttribute("aria-valuemin"), spin.GetAttribute("aria-valuemax"), spin.GetAttribute("aria-valuenow")).ShouldBe(("-12", "12", "0"));
        spin.GetAttribute("aria-valuetext").ShouldBe("0 semitones");
        spin.GetAttribute("data-help-id").ShouldBe("practice-transpose");
        spin.HasAttribute("data-ss-spin").ShouldBeTrue();
        page.Find($"#{spin.GetAttribute("aria-labelledby")}").TextContent.ShouldContain("Transpose");
        spin.TextContent.Trim().ShouldBe("0");
    }

    [Theory]
    [InlineData(2, "+2", "+2 semitones")]
    [InlineData(1, "+1", "+1 semitone")]
    [InlineData(-1, "-1", "-1 semitone")]
    [InlineData(-3, "-3", "-3 semitones")]
    public void Value_IsSignedText_AndSpokenWithUnit(int semitones, string text, string spoken)
    {
        MixWith(At(100, semitones));

        var page = RenderAt(withMix: true);

        page.WaitForAssertion(() => page.Find("[data-testid=practice-transpose]").TextContent.Trim().ShouldBe(text));
        page.Find("[data-testid=practice-transpose]").GetAttribute("aria-valuetext").ShouldBe(spoken);
        page.Find("[data-testid=practice-panel]").GetAttribute("data-transpose").ShouldBe(semitones.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Buttons_StepOneSemitone_AndClampAtTwelve()
    {
        var page = RenderAt();

        for (var i = 0; i < 13; i++)
        {
            page.Find("[data-testid=practice-transpose-up]").Click();
        }

        page.Find("[data-testid=practice-transpose]").GetAttribute("aria-valuenow").ShouldBe("12");
        page.Find("[data-testid=practice-transpose-down]").Click();
        page.Find("[data-testid=practice-transpose]").GetAttribute("aria-valuenow").ShouldBe("11");
        page.Find("[data-testid=practice-transpose-down]").GetAttribute("aria-label").ShouldBe("Down one semitone");
        page.Find("[data-testid=practice-transpose-up]").GetAttribute("aria-label").ShouldBe("Up one semitone");
    }

    [Theory]
    [InlineData("ArrowUp", "1")]
    [InlineData("ArrowDown", "-1")]
    [InlineData("PageUp", "0")]
    public void ArrowKeys_OnTheSpinbutton_ChangeTheKey(string key, string expected)
    {
        var page = RenderAt();

        page.Find("[data-testid=practice-transpose]").KeyDown(key);

        page.Find("[data-testid=practice-transpose]").GetAttribute("aria-valuenow").ShouldBe(expected);
    }

    [Fact]
    public void Transpose_IsSent_AndAppliesWhileTheTrainerRuns()
    {
        var page = RenderAt();
        RaiseStatus(Running(70));

        page.WaitForAssertion(() => page.Find("[data-testid=practice-speed]").GetAttribute("aria-readonly").ShouldBe("true"));
        page.Find("[data-testid=practice-transpose-up]").Click();

        Engine.Received(1).SetPracticeAsync(At(100, 1));
        page.Find("[data-testid=practice-transpose-up]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Expert_NumberEntry_MirrorsTheSpinbutton_AndClamps()
    {
        var page = RenderAt(ExperienceLevel.Expert);
        var input = page.Find("[data-testid=practice-transpose-input]");
        (input.GetAttribute("type"), input.GetAttribute("min"), input.GetAttribute("max"), input.GetAttribute("value")).ShouldBe(("number", "-12", "12", "0"));

        input.Change("-5");
        page.Find("[data-testid=practice-transpose]").TextContent.Trim().ShouldBe("-5");
        page.Find("[data-testid=practice-transpose-input]").Change("40");
        page.Find("[data-testid=practice-transpose]").TextContent.Trim().ShouldBe("+12");
        page.Find("[data-testid=practice-transpose-input]").Change("x");

        page.Find("[data-testid=practice-transpose]").TextContent.Trim().ShouldBe("+12");
        page.Find("[data-testid=practice-transpose-input]").GetAttribute("aria-label").ShouldBe("Transpose in semitones");
        Engine.Received(1).SetPracticeAsync(Arg.Any<PracticeSettingsDto>());
    }
}
