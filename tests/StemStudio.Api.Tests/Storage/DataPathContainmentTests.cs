using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Generations;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.LibraryCare;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Persistence;

namespace StemStudio.Api.Tests.Storage;

/// <summary>
/// Security review 013 item 1: stored paths read from the database are resolved with <c>GetFullPath</c> +
/// <c>StartsWith(root + separator)</c>, so a crafted path never serves or reads a file outside the data directory.
/// </summary>
public sealed class DataPathContainmentTests : ApiTestBase
{
    private readonly LibraryFixture _library;
    private readonly List<FileInfo> _outside = [];

    public DataPathContainmentTests() => _library = new LibraryFixture(Factory);

    [Theory]
    [InlineData("tracks/a/b.flac", true)]
    [InlineData("../outside.txt", false)]
    [InlineData("tracks/../../outside.txt", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("..", false)]
    public void ContainedPath_RefusesPathsOutsideTheRoot(string relative, bool inside)
    {
        var data = new DataDirectory(Factory.DataDir);

        var path = data.ContainedPath(relative);

        (path is not null).ShouldBe(inside);
        if (inside)
        {
            path.ShouldBe(Path.Combine(Factory.DataDir, "tracks", "a", "b.flac"));
        }
        else
        {
            Should.Throw<InvalidOperationException>(() => data.RequireContainedPath(relative));
        }
    }

    [Fact]
    public async Task TakeStorage_NeverResolvesOutsideTheDataDirectory()
    {
        var outside = await OutsideFileAsync();
        var storage = new TakeStorage(new DataDirectory(Factory.DataDir));

        storage.Find("../" + outside.Name).ShouldBeNull();
        Should.Throw<InvalidOperationException>(() => storage.FullPath("../" + outside.Name));
    }

    [Fact]
    public async Task TakeVocal_OutsideTheDataDirectory_IsNotServed()
    {
        var outside = await OutsideFileAsync();
        var track = await _library.TrackAsync(detail: false);
        var generation = await _library.GenerationAsync(track, _ => "../" + outside.Name);

        using var response = await Client.GetAsync(new Uri(ApiRoutes.TakeVocal(generation.Id, 1), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VoiceSample_OutsideTheDataDirectory_IsNotServed()
    {
        var outside = await OutsideFileAsync();
        var profile = await _library.VoiceProfileAsync();
        await Factory.WithScopeAsync(sp => sp.GetRequiredService<StemStudioDbContext>().Database
            .ExecuteSqlRawAsync("UPDATE VoiceProfiles SET SamplePath = {0}", ["../" + outside.Name], Ct));

        using var response = await Client.GetAsync(new Uri(ApiRoutes.VoiceSample(profile.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A file next to the data directory (what a traversal would reach).</summary>
    private async Task<FileInfo> OutsideFileAsync()
    {
        var file = new FileInfo(Factory.DataDir + "-secret-" + Guid.NewGuid().ToString("N") + ".xml");
        await File.WriteAllTextAsync(file.FullName, "<key/>", Ct);
        _outside.Add(file);
        return file;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _outside.ForEach(file => file.Delete());
        base.Dispose(disposing);
    }
}
