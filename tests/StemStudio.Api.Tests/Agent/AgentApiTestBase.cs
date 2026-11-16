using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Agent.Ports;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;

namespace StemStudio.Api.Tests.Agent;

/// <summary>
/// Base of the agent endpoint tests: the API with NSubstitute substitutes of the agent library's inbound ports
/// (cross-owner rule: the backend depends only on the Phase 0 public API of StemStudio.Agent).
/// </summary>
public abstract class AgentApiTestBase : IDisposable
{
    private protected AgentApiTestBase()
    {
        StatusService.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentStatuses.Ready());
        Factory = new ApiFactory { ConfigureServices = Register };
        Client = Factory.CreateClient();
    }

    private protected IAgentRunService RunService { get; } = Substitute.For<IAgentRunService>();

    private protected IAgentStatusService StatusService { get; } = Substitute.For<IAgentStatusService>();

    private protected IUiToolGateway UiGateway { get; } = Substitute.For<IUiToolGateway>();

    private protected ILlmGpuRelease GpuRelease { get; } = Substitute.For<ILlmGpuRelease>();

    private protected ApiFactory Factory { get; }

    private protected HttpClient Client { get; }

    private protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    private protected T Service<T>()
        where T : notnull => Factory.Services.GetRequiredService<T>();

    private protected async Task<ConversationDto> CreateConversationAsync(string? title = null)
    {
        using var response = await Client.PostAsJsonAsync(ApiRoutes.AgentConversations, new CreateConversationRequest { Title = title }, Ct);
        return await response.BodyAsync<ConversationDto>();
    }

    private protected Task<HttpResponseMessage> SendMessageAsync(Guid conversationId, string text = "Mute the drums", string page = "mixer") =>
        Client.PostAsJsonAsync(ApiRoutes.AgentMessages(conversationId), new SendAgentMessageRequest { Text = text, Context = new PageContextDto { Page = page } }, Ct);

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the client and the factory.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }

    private void Register(IServiceCollection services)
    {
        services.AddSingleton(RunService);
        services.AddSingleton(StatusService);
        services.AddSingleton(UiGateway);
        services.AddSingleton(GpuRelease);
    }
}
