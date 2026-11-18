using StemStudio.Web.Library;

namespace StemStudio.Web.Tests.Library;

/// <summary><see cref="OwnNavigations"/>: own echoes, repeats, Back/Forward to own entries and foreign locations (022 FR-026).</summary>
public sealed class OwnNavigationsTests
{
    private readonly OwnNavigations _own = new();

    [Fact]
    public void AnOwnStateSeenTheFirstTime_IsAnEcho_InAnyOrder()
    {
        var (first, second, third) = (_own.Next(), _own.Next(), _own.Next());

        _own.IsEcho(second).ShouldBeTrue();
        _own.IsEcho(first).ShouldBeTrue();
        _own.IsEcho(third).ShouldBeTrue();
    }

    [Fact]
    public void TheLocationTakenOverLast_ReportedAgain_IsARepeat()
    {
        var state = _own.Next();
        _own.IsEcho(state).ShouldBeTrue();

        _own.IsEcho(state).ShouldBeTrue();
    }

    [Fact]
    public void AnOwnStateThatCameBackBefore_IsBackOrForward()
    {
        var (pushed, cleared) = (_own.Next(), _own.Next());
        _own.IsEcho(pushed);
        _own.IsEcho(cleared);

        _own.IsEcho(pushed).ShouldBeFalse();
        _own.IsEcho(cleared).ShouldBeFalse();
        _own.IsEcho(cleared).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("someone-else")]
    [InlineData("ss-search:0123:1")]
    public void AStateThatIsNotOwn_IsGenuine_AndEndsTheRepeat(string? state)
    {
        var own = _own.Next();
        _own.IsEcho(own);

        _own.IsEcho(state).ShouldBeFalse();
        _own.IsEcho(own).ShouldBeFalse();
    }

    [Fact]
    public void AnOwnPrefixWithoutANumber_IsGenuine()
    {
        var own = _own.Next();

        _own.IsEcho(own[..own.LastIndexOf(':')] + ":x").ShouldBeFalse();
    }

    [Fact]
    public void StatesOfAnotherInstance_AreGenuine()
    {
        var other = new OwnNavigations().Next();

        _own.IsEcho(other).ShouldBeFalse();
    }

    [Fact]
    public void AfterManyArrivals_TheOldestAreForgotten_AndCountAsGenuine()
    {
        var states = Enumerable.Range(0, 70).Select(_ => _own.Next()).ToList();
        var late = states[0];
        foreach (var state in states.Skip(1))
        {
            _own.IsEcho(state).ShouldBeTrue();
        }

        _own.IsEcho(late).ShouldBeFalse();
        _own.IsEcho(states[1]).ShouldBeFalse();
        _own.IsEcho(states[^1]).ShouldBeFalse();
    }
}
