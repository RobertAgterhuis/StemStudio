using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using StemStudio.Agent.Backends.Fakes;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tests.Fixtures;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>Builds an <see cref="AgentRunner"/> over in-memory fakes with one conversation and one queued run.</summary>
public sealed class RunnerHarness
{
    public static readonly DateTimeOffset Start = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    public RunnerHarness(IChatClient client, IEnumerable<AgentToolDescriptor> tools, string command = "Mute the drums", string page = "tracks")
    {
        Client = client;
        Session = new FixedChatSession(client);
        Actions = Substitute.For<IStemStudioActions>();
        Actions.Generations.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(new GenerationStatusDto
        {
            State = GenerationAvailabilityDto.Ready,
            Voice = new VoiceServiceStatusDto { State = GenerationAvailabilityDto.Ready },
        });
        Registry = new AgentToolRegistry([new FixedToolProvider(tools)]);
        Command = Message(1, MessageRoleDto.User, command, page);
        Run = NewRun(Command.Id);
    }

    public IChatClient Client { get; }

    public IAgentChatSession Session { get; set; }

    public FakeTimeProvider Time { get; } = new(Start);

    public InMemoryRunStore Runs { get; } = new();

    /// <summary>The run store the runner uses (default: <see cref="Runs"/>).</summary>
    public IRunStore? RunStore { get; set; }

    public Microsoft.Extensions.Logging.ILogger? Logger { get; init; }

    public InMemoryConversationStore Conversations { get; } = new();

    public InMemorySettingsStore Settings { get; } = new(AgentSettings.Defaults(AgentBackendDto.Fake, Start));

    public InMemoryUndoStore Undo { get; } = new();

    public RecordingEventSink Events { get; } = new();

    public FakeUiGateway Ui { get; } = new();

    public IStemStudioActions Actions { get; }

    public ILyricAssistActions? LyricAssist { get; set; }

    public AgentToolRegistry Registry { get; set; }

    public Guid ConversationId { get; } = Guid.NewGuid();

    public ConversationMessage Command { get; private set; }

    public AgentRun Run { get; private set; }

    public ScriptedChatClient Script => (ScriptedChatClient)Client;

    public static RunnerHarness Scripted(IEnumerable<AgentToolDescriptor> tools, params ScriptedTurn[] turns) => new(new ScriptedChatClient(turns), tools);

    public void UseSession(IAgentChatSession session) => Session = session;

    public IUiToolGateway? UiGateway { get; private set; }

    public void UseUi(IUiToolGateway gateway) => UiGateway = gateway;

    public void UseBackend(AgentBackendDto backend, TokenUsage? usage = null) => Run = Run with { Backend = backend, Usage = usage ?? Run.Usage };

    public void SetCommandContext(PageContextDto context) => Command = Command with { Context = context };

    public void SetCommandText(string text) => Command = Command with { Text = text };

    public AgentServices Services() => new()
    {
        Runs = RunStore ?? Runs,
        Conversations = Conversations,
        Settings = Settings,
        Events = Events,
        Actions = Actions,
        LyricAssist = LyricAssist,
        Ui = UiGateway ?? Ui,
        Registry = Registry,
        Chat = Session,
        Undo = Undo,
        Time = Time,
        Logger = Logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
    };

    public AgentRunner Runner() => new(Services());

    public void Limits(Func<AgentLimitsDto, AgentLimitsDto> change) =>
        Settings.SaveAsync(Settings.Current with { Limits = change(Settings.Current.Limits) }, CancellationToken.None);

    /// <summary>Seeds earlier messages; the command is re-numbered to come last.</summary>
    public void History(params (MessageRoleDto Role, string Text)[] messages)
    {
        var seq = 1;
        foreach (var (role, text) in messages)
        {
            Conversations.AppendMessageAsync(Message(seq++, role, text, Command.Context.Page), CancellationToken.None);
        }

        Command = Command with { Seq = seq };
    }

    public async Task<AgentRun> ExecuteAsync()
    {
        await SeedAsync();
        await Runner().ExecuteAsync(Run.Id, TestContext.Current.CancellationToken);
        return (await Runs.GetRunAsync(Run.Id, CancellationToken.None))!;
    }

    public async Task SeedAsync()
    {
        if (await Conversations.GetAsync(ConversationId, CancellationToken.None) is not null)
        {
            return;
        }

        await Conversations.CreateAsync(new Conversation { Id = ConversationId, Title = "t", CreatedAt = Start, UpdatedAt = Start, ActiveRunId = Run.Id }, CancellationToken.None);
        await Conversations.AppendMessageAsync(Command, CancellationToken.None);
        await Runs.CreateRunAsync(Run, CancellationToken.None);
    }

    public Task<AgentRun?> CurrentRunAsync() => Runs.GetRunAsync(Run.Id, CancellationToken.None);

    public ConversationMessage Message(int seq, MessageRoleDto role, string text, string page) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = ConversationId,
        Seq = seq,
        Role = role,
        Text = text,
        Context = new PageContextDto { Page = page },
        CreatedAt = Start,
    };

    private AgentRun NewRun(Guid messageId) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = ConversationId,
        UserMessageId = messageId,
        Status = AgentRunStatusDto.Queued,
        Backend = AgentBackendDto.Fake,
        Model = "scripted",
        Tainted = false,
        ModelTurns = 0,
        ToolCalls = 0,
        InvalidCalls = 0,
        StartedAt = Start,
    };
}

/// <summary>Test tool descriptors with handlers.</summary>
public static class TestTools
{
    public static ScriptedCall Call(string name, string? json = null) =>
        new() { Name = name, Arguments = json is null ? null : JsonDocument.Parse(json).RootElement.Clone() };

    public static ScriptedTurn Turn(params ScriptedCall[] calls) => new() { Calls = calls };

    public static ScriptedTurn Say(string text) => new() { Text = text };

    public static AgentToolDescriptor Server(string name, Func<ToolInvocation, ToolResult> handler, ToolRiskDto risk = ToolRiskDto.Read, ToolSchema? schema = null) => new()
    {
        Name = name,
        Feature = "001",
        Kind = ToolKindDto.Server,
        Risk = risk,
        UndoKind = UndoKindDto.None,
        Group = ToolGroup.Core,
        Availability = ToolAvailability.Always,
        Title = "Test " + name,
        Description = "Test tool " + name,
        Schema = schema ?? ToolSchema.Empty,
        Handler = (invocation, _) => Task.FromResult(handler(invocation)),
    };

    public static AgentToolDescriptor Echo(string name = "echo") =>
        Server(name, invocation => ToolResult.Of(new JsonObject { ["echo"] = invocation.Text("value") ?? "none" }), schema: ToolSchema.Of(Param.Text("value", 1, 50).Optional()));

    public static AgentToolDescriptor Ui(string name, ToolRiskDto risk = ToolRiskDto.Change) => Server(name, _ => throw new InvalidOperationException(), risk) with
    {
        Kind = ToolKindDto.Ui,
        Handler = null,
    };
}
