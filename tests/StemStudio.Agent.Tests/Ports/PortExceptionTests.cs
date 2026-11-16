using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Tests.Ports;

/// <summary>Skeleton tests (architect, 011 Phase 0 T004): the exceptions the ports use for problem responses.</summary>
public sealed class PortExceptionTests
{
    [Fact]
    public void ActionException_CarriesStatusAndProblemCode()
    {
        var notFound = new AgentActionException(404, "not-found", "The track does not exist.");

        (notFound.StatusCode, notFound.ProblemCode, notFound.Message, notFound.IsNotFound).ShouldBe((404, "not-found", "The track does not exist.", true));
        new AgentActionException(409, "conflict", "Busy.").IsNotFound.ShouldBeFalse();
    }

    [Fact]
    public void ActionException_DefaultsMeanApiNotReachable()
    {
        var cause = new HttpRequestException("refused");

        new AgentActionException().StatusCode.ShouldBe(0);
        new AgentActionException().Message.ShouldBe("StemStudio could not be reached.");
        new AgentActionException("Timeout.").ProblemCode.ShouldBeNull();
        new AgentActionException("Timeout.", cause).InnerException.ShouldBe(cause);
    }

    [Fact]
    public void ConflictException_CarriesTheProblemCode()
    {
        var cause = new InvalidOperationException("x");

        new AgentConflictException("run-active", "A command is already running.").ProblemCode.ShouldBe("run-active");
        new AgentConflictException().ProblemCode.ShouldBe("conflict");
        new AgentConflictException("Busy.").Message.ShouldBe("Busy.");
        var wrapped = new AgentConflictException("Busy.", cause);
        (wrapped.ProblemCode, wrapped.InnerException).ShouldBe(("conflict", (Exception)cause));
    }
}
