using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Persistence.Agent;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence;

/// <summary>Registers the SQLite context and the repositories.</summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>Adds <see cref="StemStudioDbContext"/> (SQLite) and the scoped repositories.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="connectionString">SQLite connection string, e.g. <c>Data Source=/data/stemstudio.db</c>.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddStemStudioPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<StemStudioDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ITrackRepository, TrackRepository>();
        services.AddScoped<IMixRepository, MixRepository>();
        services.AddScoped<IArtistRepository, ArtistRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IStyleRepository, StyleRepository>();
        services.AddScoped<IGenerationRepository, GenerationRepository>();
        services.AddScoped<ILyricsSheetRepository, LyricsSheetRepository>();
        services.AddScoped<IVoiceProfileRepository, VoiceProfileRepository>();
        services.AddScoped<IAudioHashRepository, AudioHashRepository>();
        services.AddScoped<IAgentSecretStore, AgentSecretStore>();
        services.AddScoped<ILyricAssistRepository, LyricAssistRepository>();
        services.AddScoped<ILyricSuggestionRepository, LyricSuggestionRepository>();
        services.AddScoped<ILyricsRevisionRepository, LyricsRevisionRepository>();
        services.AddScoped<ILyricWordRepository, LyricWordRepository>();
        services.AddScoped<ILyricsCorpusSource, LyricsCorpusSource>();
        services.AddScoped<ISectionRepository, SectionRepository>();
        services.AddScoped<IScoreViewRepository, ScoreViewRepository>();
        services.AddScoped<ISetlistRepository, SetlistRepository>();
        services.AddScoped<IPracticeRepository, PracticeRepository>();
        services.AddScoped<ITrackAnalysisRepository, TrackAnalysisRepository>();
        return services;
    }
}
