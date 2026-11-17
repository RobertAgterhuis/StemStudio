using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StemStudio.Api.Agent.Loopback;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Composition hook of feature 023 (lyric writing assistant): dictionaries, assist service and runner, suggestions, revisions,
/// loopback actions and the endpoints. The only place 023 registers or maps anything (no edit of <see cref="ApiComposition"/>).
/// </summary>
public static class LyricAssistRegistration
{
    /// <summary>Registers the 023 services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddLyricAssist(this IServiceCollection services)
    {
        AddLanguageData(services);
        AddRunner(services);
        AddEndpointServices(services);
        return services;
    }

    /// <summary>Maps the 023 endpoints (tag <c>lyric-assist</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapLyricAssistEndpoints(this IEndpointRouteBuilder app) =>
        app.MapAssistEndpoints().MapSuggestionEndpoints().MapLyricLanguageEndpoints().MapOwnWorkOverrideEndpoints();

    // Dictionaries, CMUdict, phonology, corpus, the verification port and the ai-agent seam (singletons, loaded lazily).
    private static void AddLanguageData(IServiceCollection services)
    {
        services.AddOptions<LyricAssistHostOptions>().BindConfiguration(LyricAssistHostOptions.SectionName);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LyricAssistHostOptions>>().Value);
        services.AddSingleton<ISpellChecker>(_ => new HunspellSpellChecker(HunspellSpellChecker.DefaultRoot));
        services.AddSingleton<LyricsCorpusProvider>();
        services.AddSingleton(_ => new CmuPronunciationLexicon(CmuPronunciationLexicon.DefaultPath));
        services.AddSingleton<IPronunciationLexicon>(sp => sp.GetRequiredService<CmuPronunciationLexicon>());
        services.AddSingleton<IWordList>(sp => new DictionaryWordList(DictionaryWordList.DefaultDutchPath, sp.GetRequiredService<CmuPronunciationLexicon>()));
        services.AddSingleton<LyricPhonology>();
        services.AddSingleton<RhymeFinder>();
        services.AddSingleton<StemStudio.Agent.LyricAssist.ILyricVerification, LyricVerification>();
        StemStudio.Agent.LyricAssist.LyricAssistServiceCollectionExtensions.AddLyricAssistAgent(services);
        services.TryAddSingleton<StemStudio.Agent.Ports.ILyricAssistActions>(sp => new LoopbackLyricAssistActions(sp.GetRequiredService<LoopbackClient>()));
    }

    // The background runner, start-up recovery and what a run uses.
    private static void AddRunner(IServiceCollection services)
    {
        services.AddSingleton<LyricAssistRunner>();
        services.AddHostedService<LyricAssistStartupRecovery>();
        services.AddScoped(sp => new LyricAssistStores
        {
            Sheets = sp.GetRequiredService<ILyricsSheetRepository>(),
            Assists = sp.GetRequiredService<ILyricAssistRepository>(),
            Suggestions = sp.GetRequiredService<ILyricSuggestionRepository>(),
            Words = sp.GetRequiredService<ILyricWordRepository>(),
        });
        services.AddScoped<AssistEventLog>();
        services.AddScoped<LyricModelLayer>();
        services.AddScoped<LyricAssistExecutor>();
        services.AddScoped<LyricsSheetChanges>();
        services.AddScoped<ILyricAssistCanceller, LyricAssistCanceller>();
    }

    private static void AddEndpointServices(IServiceCollection services)
    {
        services.AddScoped<LyricAssistEventWriter>();
        services.AddScoped<SuggestionService>();
        services.AddScoped<LyricsRevisionService>();
        services.AddScoped<AssistPreferencesService>();
        services.AddScoped<LyricLanguageService>();
        services.AddScoped<LyricAssistStatusService>();
        services.AddScoped<OwnWorkOverrideService>();
        services.AddScoped(sp => new LyricAssistService(new LyricAssistServiceDeps
        {
            Stores = sp.GetRequiredService<LyricAssistStores>(),
            ModelLayer = sp.GetRequiredService<LyricModelLayer>(),
            Corpus = sp.GetRequiredService<LyricsCorpusProvider>(),
            Imitation = sp.GetRequiredService<Styles.LibraryStyleGuard>(),
            Events = sp.GetRequiredService<AssistEventLog>(),
            Runner = sp.GetRequiredService<LyricAssistRunner>(),
            Clock = sp.GetRequiredService<TimeProvider>(),
        }));
    }
}
