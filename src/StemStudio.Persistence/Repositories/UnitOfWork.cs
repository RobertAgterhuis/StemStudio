using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace StemStudio.Persistence.Repositories;

/// <summary>Saves changes and translates SQLite unique-constraint violations into <see cref="DuplicateKeyException"/>.</summary>
internal static class UnitOfWork
{
    private const int SqliteConstraintUnique = 2067;
    private const int SqliteConstraintPrimaryKey = 1555;

    /// <summary>Saves all tracked changes.</summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">A unique index was violated; the failed changes are discarded.</exception>
    public static async Task SaveAsync(DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.ChangeTracker.Clear();
            throw new DuplicateKeyException("A record with the same unique key already exists.", exception);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey };
}
