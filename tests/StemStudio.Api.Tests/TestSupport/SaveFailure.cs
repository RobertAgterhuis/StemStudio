using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.TestSupport;

/// <summary>Makes the API's saves fail while <see cref="When"/> matches the pending changes (a full disk, a locked database).</summary>
internal sealed class SaveFailure : SaveChangesInterceptor
{
    /// <summary>Fails a save whose change tracker matches; null = never.</summary>
    public Func<DbContext, bool>? When { get; set; }

    /// <summary>How many saves failed.</summary>
    public int Failures { get; private set; }

    /// <summary>Adds the interceptor to the API's database context.</summary>
    public void AddTo(IServiceCollection services) => services.ConfigureDbContext<StemStudioDbContext>(options => options.AddInterceptors(this));

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && When?.Invoke(context) == true)
        {
            Failures++;
            throw new DbUpdateException("The database is full.");
        }

        return ValueTask.FromResult(result);
    }
}
