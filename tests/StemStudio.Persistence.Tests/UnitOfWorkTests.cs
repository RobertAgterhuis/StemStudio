using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Persistence.Tests;

/// <summary>Every branch of the unique-violation translation, without relying on a timed race.</summary>
public sealed class UnitOfWorkTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(2067)]
    [InlineData(1555)]
    public async Task UniqueOrPrimaryKeyViolation_BecomesDuplicateKey(int extendedCode)
    {
        var failure = new DbUpdateException("x", new SqliteException("constraint", 19, extendedCode));
        await using var context = new FailingContext(failure);

        var error = await Should.ThrowAsync<DuplicateKeyException>(() => UnitOfWork.SaveAsync(context, Ct));

        error.InnerException.ShouldBeSameAs(failure);
    }

    public static TheoryData<DbUpdateException> OtherFailures() =>
    [
        new DbUpdateException("foreign key", new SqliteException("constraint", 19, 787)),
        new DbUpdateException("other", new InvalidOperationException("not sqlite")),
        new DbUpdateException("no inner"),
    ];

    [Theory]
    [MemberData(nameof(OtherFailures))]
    public async Task OtherFailures_AreRethrownUnchanged(DbUpdateException failure)
    {
        await using var context = new FailingContext(failure);

        var error = await Should.ThrowAsync<DbUpdateException>(() => UnitOfWork.SaveAsync(context, Ct));

        error.ShouldBeSameAs(failure);
    }

    private sealed class FailingContext(DbUpdateException failure) : DbContext
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromException<int>(failure);

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite("Data Source=:memory:");
    }
}
