using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StemStudio.Persistence;

/// <summary>Creates the context for <c>dotnet ef</c> (migrations); never used at runtime.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StemStudioDbContext>
{
    /// <inheritdoc />
    public StemStudioDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite("Data Source=stemstudio-design.db").Options);
}
