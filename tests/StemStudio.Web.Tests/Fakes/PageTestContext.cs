using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Radzen;
using StemStudio.Contracts.Agent;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Help;
using StemStudio.Web.Interop;
using StemStudio.Web.Lyrics;
using StemStudio.Web.Preferences;
using StemStudio.Web.Services;

namespace StemStudio.Web.Tests.Fakes;

/// <summary>bUnit context with Radzen, loose JS interop and substitutes for every frontend port.</summary>
public abstract class PageTestContext : BunitContext
{
    private static readonly Lazy<HelpCatalog> RealHelp = new(() => HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly));

    // Polling tests release ManualPollingTimer continuations on the thread pool; under a fully parallel run the pool
    // can be busy for more than bUnit's 1 s default, which made WaitForAssertion flaky (007 T067). Correct waits
    // still finish at once; only a starved run waits longer.
    static PageTestContext() => DefaultWaitTimeout = TimeSpan.FromSeconds(10);

    protected PageTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddRadzenComponents();
        Services.AddSingleton(Api);
        Services.AddSingleton(Engine);
        Services.AddSingleton(Confirm);
        Services.AddSingleton(Substitute.For<IKeyboardShortcuts>());
        Services.AddSingleton<IPollingTimer>(Timer);
        Services.AddSingleton(Options.Create(new StemStudioWebOptions()));
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton<IWebInterop>(Interop);
        Services.AddSingleton(Recorder);
        Services.AddSingleton<IPreferencesStore>(Store);
        Services.AddSingleton<PreferencesState>();
        Services.AddSingleton<ToastService>();
        Services.AddSingleton<IHelpCatalog>(_ => RealHelp.Value);
        Services.AddSingleton<IHelpSearch, HelpSearch>();
        Services.AddSingleton(Substitute.For<ITourLauncher>());
        Services.AddScoped<TourService>();
        Services.AddScoped<PageCommands>();
        Services.AddScoped<ShellOverlays>();
        Services.AddScoped<HelpContext>();
        Services.AddScoped<KaraokePreferences>();
        Services.AddScoped<StemStudio.Web.Generation.GenerationStatusSource>();
        Services.AddSingleton(AgentApi);
        Services.AddScoped<AgentPanelState>();
        GenerativeDefaults();
        AgentDefaults();
    }

    /// <summary>The agent is ready, has no conversations and streams <see cref="AgentEvents"/> for every run (011).</summary>
    private void AgentDefaults()
    {
        AgentApi.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(AgentData.Status());
        AgentApi.ListConversationsAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>()).Returns([]);
        AgentApi.CreateConversationAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(AgentData.Conversation());
        AgentApi.GetConversationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => AgentData.Conversation(call.Arg<Guid>()));
        AgentApi.SendMessageAsync(Arg.Any<Guid>(), Arg.Any<SendAgentMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AgentMessageAcceptedDto { RunId = AgentData.RunId, MessageId = AgentData.MessageId });
        AgentApi.StreamRunEventsAsync(Arg.Any<Guid>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call => AgentEvents.Read(call.ArgAt<int?>(1), call.ArgAt<CancellationToken>(2)));
    }

    /// <summary>The agent API port (backend API 1.10.0), substituted.</summary>
    protected IAgentApiClient AgentApi { get; } = Substitute.For<IAgentApiClient>();

    /// <summary>The event stream every run of <see cref="AgentApi"/> returns.</summary>
    protected AgentStream AgentEvents { get; } = new();

    protected AgentPanelState Panel => Services.GetRequiredService<AgentPanelState>();

    /// <summary>Generation features answer "not installed" and empty lists unless a test says otherwise (009/010).</summary>
    private void GenerativeDefaults()
    {
        Api.GetGenerationStatusAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.NotInstalled());
        Api.ListStylesAsync(Arg.Any<CancellationToken>()).Returns(GenerationData.Styles());
        Api.ListGenerationsAsync(Arg.Any<GenerationQuery>(), Arg.Any<CancellationToken>()).Returns([]);
        Api.ListLyricsSheetsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([]);
        Api.ListVoiceProfilesAsync(Arg.Any<CancellationToken>()).Returns([]);
        Api.GetVoiceConsentAsync(Arg.Any<CancellationToken>()).Returns(VoiceData.Statement());
        Recorder.UnavailableReasonAsync().Returns((string?)null);
    }

    protected IStemStudioApiClient Api { get; } = Substitute.For<IStemStudioApiClient>();

    protected IAudioEngine Engine { get; } = Substitute.For<IAudioEngine>();

    protected IConfirmService Confirm { get; } = Substitute.For<IConfirmService>();

    protected ManualPollingTimer Timer { get; } = new();

    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));

    protected FakeWebInterop Interop { get; } = new();

    /// <summary>The voice recorder port (MediaRecorder in web-interop.js), substituted.</summary>
    protected IVoiceRecorder Recorder { get; } = Substitute.For<IVoiceRecorder>();

    protected FakePreferencesStore Store { get; } = new();

    protected PreferencesState Preferences => Services.GetRequiredService<PreferencesState>();

    protected static ApiException Problem(System.Net.HttpStatusCode status, string title, string? detail = null) =>
        new(new ApiError(status, title, detail, new Dictionary<string, string[]>()));

    /// <summary>Renders a component under a cascaded experience level.</summary>
    protected IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>> RenderAtLevel<TComponent>(
        StemStudio.Web.Experience.ExperienceLevel level, Action<ComponentParameterCollectionBuilder<TComponent>> parameters)
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        Render<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>(p => p
            .Add(c => c.Value, new StemStudio.Web.Experience.ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent(parameters));

    protected string NavigatedTo => Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri;
}
