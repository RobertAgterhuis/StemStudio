using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Configurations;

namespace StemStudio.Persistence;

/// <summary>
/// EF Core context for tracks, stems and mixes (SQLite). Entity configurations live in this
/// assembly and are applied automatically; see specs/001-stemstudio-mvp/data-model.md.
/// </summary>
/// <param name="options">Context options (provider and connection string).</param>
public sealed class StemStudioDbContext(DbContextOptions<StemStudioDbContext> options) : DbContext(options)
{
    /// <summary>Imported tracks.</summary>
    public DbSet<Track> Tracks => Set<Track>();

    /// <summary>Saved mixes.</summary>
    public DbSet<Mix> Mixes => Set<Mix>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StemStudioDbContext).Assembly);
    }
}
