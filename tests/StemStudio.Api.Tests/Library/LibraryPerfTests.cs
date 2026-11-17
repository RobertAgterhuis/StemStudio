using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Library;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Persistence.Repositories;
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

    [Fact]
    public async Task FolderViewsWithin100ms_RebuildWithin200ms_SearchWithin50ms()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await LibrarySeed.SeedAsync(factory);
        await factory.Services.GetRequiredService<LibraryFilingBackfill>().RunOnceAsync(Ct);

        var rebuilds = await RebuildTimesAsync(factory, 5);
        var urls = await FolderUrlsAsync(client);
        var times = new List<double>();
        for (var round = 0; round < 20; round++)
        {
            foreach (var url in urls)
            {
                times.Add(await TimeAsync(() => client.GetAsync(new Uri(url, UriKind.Relative), Ct)));
            }
        }

        Percentile(times, 0.95).ShouldBeLessThan(100);
        Percentile(rebuilds, 0.5).ShouldBeLessThan(200);
        Percentile(await SearchTimesAsync(client), 0.95).ShouldBeLessThan(50);
    }

    private static async Task<List<double>> SearchTimesAsync(HttpClient client)
    {
        string[] queries = ["m", "met", "snare", "artist 01", "the art", "zzzz", "song 12", "drums", "lead vocals", "album 3 bass"];
        (await client.GetAsync(new Uri(ApiRoutes.LibrarySearch + "?q=warm", UriKind.Relative), Ct)).Dispose();
        var times = new List<double>();
        for (var round = 0; round < 10; round++)
        {
            foreach (var query in queries)
            {
                times.Add(await TimeAsync(() => client.GetAsync(new Uri($"{ApiRoutes.LibrarySearch}?q={Uri.EscapeDataString(query)}", UriKind.Relative), Ct)));
            }
        }

        return times;
    }

    [Fact]
    [Trait("Category", "Seed")]
    public async Task Seed_WritesTheE2EDatabaseFixtures()
    {
        var output = Environment.GetEnvironmentVariable(LibrarySeed.OutputVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(output), $"Set {LibrarySeed.OutputVariable} to write the fixtures.");
        Directory.CreateDirectory(output!);
        using var factory = new ApiFactory();
        await LibrarySeed.SeedAsync(factory);
        var pre022 = Path.Combine(output!, LibrarySeed.Pre022File);
        await LibrarySeed.CopyAsync(factory, pre022);
        await LibrarySeed.MigrateToPre022Async(pre022);
        await factory.Services.GetRequiredService<LibraryFilingBackfill>().RunOnceAsync(Ct);
        await LibrarySeed.CopyAsync(factory, Path.Combine(output!, LibrarySeed.CurrentFile));

        File.Exists(Path.Combine(output!, LibrarySeed.CurrentFile)).ShouldBeTrue();
    }

    private static async Task<List<double>> RebuildTimesAsync(ApiFactory factory, int count)
    {
        var times = new List<double>();
        for (var i = 0; i < count; i++)
        {
            var index = new LibraryIndex(factory.Services.GetRequiredService<IServiceScopeFactory>());
            times.Add(await factory.WithScopeAsync(sp => TimeAsync(() => index.GetAsync(sp.GetRequiredService<ILibraryRepository>(), Ct))));
        }

        return times;
    }

    private static async Task<List<string>> FolderUrlsAsync(HttpClient client)
    {
        var root = await (await client.GetAsync(new Uri(ApiRoutes.LibraryFolders, UriKind.Relative), Ct)).BodyAsync<LibraryFolderViewDto>();
        root.ChildrenTotal.ShouldBe(LibrarySeed.Artists);
        var artist = root.Children[0].Id;
        var album = (await (await client.GetAsync(new Uri(ApiRoutes.LibraryFolder(artist), UriKind.Relative), Ct)).BodyAsync<LibraryFolderViewDto>()).Children[0].Id;
        var song = (await (await client.GetAsync(new Uri(ApiRoutes.LibraryFolder(album), UriKind.Relative), Ct)).BodyAsync<LibraryFolderViewDto>()).Children[0].Id;
        return [ApiRoutes.LibraryFolders, ApiRoutes.LibraryFolder(artist), ApiRoutes.LibraryFolder(album), ApiRoutes.LibraryFolder(song)];
    }

    private static async Task<double> TimeAsync<T>(Func<Task<T>> action)
    {
        var watch = Stopwatch.StartNew();
        var result = await action();
        (result as IDisposable)?.Dispose();
        return watch.Elapsed.TotalMilliseconds;
    }

    private static double Percentile(List<double> values, double fraction)
    {
        var sorted = values.Order().ToList();
        return sorted[(int)Math.Ceiling(fraction * sorted.Count) - 1];
    }
}
