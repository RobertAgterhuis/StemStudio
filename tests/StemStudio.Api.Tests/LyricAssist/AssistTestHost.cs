using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StemStudio.Agent.LyricAssist;
using StemStudio.Api.LyricAssist;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.Tests.LyricAssist;

/// <summary>An assistant whose behaviour each test scripts (the model layer seen through the ai-agent seam).</summary>
internal sealed class ScriptedAssistant(Func<LyricAssistJob, ILyricAssistSink, CancellationToken, Task> run) : ILyricAssistant
{
    public List<LyricAssistJob> Jobs { get; } = [];

    public Task RunAsync(LyricAssistJob job, ILyricAssistSink sink, CancellationToken cancellationToken)
    {
        Jobs.Add(job);
        return run(job, sink, cancellationToken);
    }

    /// <summary>Reports every block as done without suggestions.</summary>
    public static ScriptedAssistant Empty() => new(async (job, sink, ct) =>
    {
        await sink.StartedAsync(new LyricModelInfo { Backend = Contracts.Agent.AgentBackendDto.Fake, Model = "scripted", UsingFallback = false }, ct);
        foreach (var block in job.Blocks)
        {
            await sink.BlockCompletedAsync(new LyricBlockResult { BlockIndex = block.Index }, ct);
        }
    });
}

/// <summary>Consumer-driven fake of <see cref="ILyricAssistantFactory"/> (both outcomes).</summary>
internal sealed class FakeAssistantFactory : ILyricAssistantFactory
{
    public ILyricAssistant? Assistant { get; set; } = ScriptedAssistant.Empty();

    public string Fix { get; set; } = "Choose Local AI (Ollama) in Settings to use the model layer.";

    public List<LyricAssistModelContext> Contexts { get; } = [];

    public ValueTask<LyricAssistantResolution> ResolveAsync(LyricAssistModelContext context, CancellationToken cancellationToken)
    {
        Contexts.Add(context);
        return ValueTask.FromResult(Assistant is { } assistant ? LyricAssistantResolution.Of(assistant) : LyricAssistantResolution.Unavailable(Fix));
    }
}

/// <summary>Spell checker over a few fixture words (a capitalised form is known when the lower-case one is).</summary>
internal sealed class FixtureSpellChecker : ISpellChecker
{
    private static readonly HashSet<string> Dutch = ["de", "wind", "zacht", "door", "het", "hoge", "riet", "fluistert", "zomer", "regel", "een", "twee", "drie", "nacht"];
    private static readonly HashSet<string> English = ["the", "cat", "sat", "on", "mat", "a", "dog", "night", "light", "love", "line", "one", "two", "three"];

    public bool IsKnown(string word, AssistLanguage language) =>
        (language == AssistLanguage.Dutch ? Dutch : English).Contains(word.ToLowerInvariant());

    public IReadOnlyList<string> Suggest(string word, AssistLanguage language, int max) =>
        word == "fluisterd" ? ["fluistert"] : word == "nigth" ? ["night"] : [];
}

/// <summary>Base of the assist endpoint tests: a host with the fake factory, the fixture spell checker and fast SSE timings.</summary>
public abstract class AssistTestBase : IDisposable
{
    private protected AssistTestBase()
    {
        Factory = new ApiFactory
        {
            Settings = new Dictionary<string, string?> { ["StemStudio:LyricAssist:StreamPollMilliseconds"] = "20", ["StemStudio:LyricAssist:KeepAliveSeconds"] = "1" },
            ConfigureServices = services =>
            {
                services.RemoveAll<ILyricAssistantFactory>();
                services.AddSingleton<ILyricAssistantFactory>(Assistants);
                services.RemoveAll<ISpellChecker>();
                services.AddSingleton<ISpellChecker>(new FixtureSpellChecker());
                Configure(services);
            },
        };
        Client = Factory.CreateClient();
    }

    private protected FakeAssistantFactory Assistants { get; } = new();

    private protected ApiFactory Factory { get; }

    private protected HttpClient Client { get; }

    private protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    private protected LyricAssistRunner Runner => Factory.Services.GetRequiredService<LyricAssistRunner>();

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Extra test services.</summary>
    /// <param name="services">Services.</param>
    private protected virtual void Configure(IServiceCollection services)
    {
    }

    /// <summary>Disposes the client and the host.</summary>
    /// <param name="disposing">True from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }

    private protected async Task<LyricsSheetDto> SheetAsync(string text, string language = "en", string title = "Song")
    {
        using var response = await Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSheets, UriKind.Relative), new LyricsInput { Title = title, Language = language, Text = text }, Ct);
        return await response.BodyAsync<LyricsSheetDto>();
    }

    private protected Task<HttpResponseMessage> CreateAsync(Guid lyricsId, CreateLyricAssistRequest request) =>
        Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsAssists(lyricsId), UriKind.Relative), request, Ct);

    private protected async Task<LyricAssistDto> RunToEndAsync(Guid lyricsId, CreateLyricAssistRequest request)
    {
        using var created = await CreateAsync(lyricsId, request);
        var summary = await created.BodyAsync<LyricAssistSummaryDto>();
        await Runner.WhenIdleAsync();
        return await GetAssistAsync(summary.AssistId);
    }

    private protected async Task<LyricAssistDto> GetAssistAsync(Guid assistId)
    {
        using var response = await Client.GetAsync(new Uri(ApiRoutes.LyricAssistById(assistId), UriKind.Relative), Ct);
        return await response.BodyAsync<LyricAssistDto>();
    }

    private protected static CreateLyricAssistRequest Check(int revision = 1) => new() { Kind = AssistKindDto.Check, ExpectedRevision = revision };
}
