using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Library;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Assets;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Library;

/// <summary>Runs the 022 performance tests alone (no other test class in parallel), close to the reference machine.</summary>
[CollectionDefinition(nameof(LibraryPerfGroup), DisableParallelization = true)]
public sealed class LibraryPerfGroup;

/// <summary>022 T032/T044/T062: library performance with 5,000 items (SC-003, SC-004).</summary>
[Collection(nameof(LibraryPerfGroup))]
[Trait("Category", "Perf")]
public sealed class LibraryPerfTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Asset[] Unfiled(int count) =>
        [.. Enumerable.Range(0, count).Select(i => Asset.Create(Guid.NewGuid(), $"item {i}", ExportProvenance(Guid.NewGuid(), title: $"Song {i % 7}", artist: $"Band {i % 3}"), T0.AddSeconds(i)))];

    [Fact]
    public async Task Backfill_FiveThousandItems_WithinTenSeconds()
    {
        using var factory = new ApiFactory();
        var backfill = factory.Services.GetRequiredService<LibraryFilingBackfill>();
        await factory.AddUnfiledAsync(Unfiled(5000));

        var watch = Stopwatch.StartNew();
        var filed = await backfill.RunOnceAsync(Ct);

        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
        filed.ShouldBe(5000);
    }
}
