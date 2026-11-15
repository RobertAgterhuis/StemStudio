using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace StemStudio.Persistence.Tests;

public sealed class SkeletonTests
{
    [Fact]
    public async Task DbContext_CanCreateSchemaOnInMemorySqlite()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<StemStudioDbContext>().UseSqlite(connection).Options;
        await using var context = new StemStudioDbContext(options);

        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        (await context.Database.CanConnectAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
    }
}
