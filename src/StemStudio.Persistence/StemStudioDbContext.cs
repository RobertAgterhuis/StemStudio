using Microsoft.EntityFrameworkCore;

namespace StemStudio.Persistence;

/// <summary>
/// EF Core context for tracks, stems and mixes (SQLite). Entity configurations live in this
/// assembly and are applied automatically; see specs/001-stemstudio-mvp/data-model.md.
/// </summary>
/// <param name="options">Context options (provider and connection string).</param>
public sealed class StemStudioDbContext(DbContextOptions<StemStudioDbContext> options) : DbContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StemStudioDbContext).Assembly);
    }
}
