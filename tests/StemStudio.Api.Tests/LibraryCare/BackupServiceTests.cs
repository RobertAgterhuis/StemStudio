using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.LibraryCare;
using StemStudio.Api.LibraryCare.Backup;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.LibraryCare.Jobs;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;
using StemStudio.TestDoubles.ClaudeKey;
using StemStudio.TestDoubles.JsonSchema;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T035: the backup job end to end on a real library (FR-001-FR-009, FR-035, SC-003; 011 CK-FR-017).</summary>
public sealed class BackupServiceTests : IDisposable
{
    private static readonly SchemaSubsetValidator Schema =
        SchemaSubsetValidator.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "013", "backup-manifest.schema.json"));

    private readonly FakeLibraryFiles _files = new();
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly LibraryFixture _library;

    public BackupServiceTests()
    {
        _factory = new ApiFactory { ConfigureServices = services => services.AddSingleton<ILibraryFiles>(_files) };
        _client = _factory.CreateClient();
        _library = new LibraryFixture(_factory);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ArchivePath => Path.Combine(_factory.BackupDir, "stemstudio-backup-20261114-120000.zip");

    [Fact]
    public async Task CompleteBackup_WritesEveryCategoryAndAValidManifest()
    {
        var track = await _library.TrackAsync();
        await _library.ExportAsync(track);
        await _library.GenerationAsync(track, id => $"generations/{id:D}/vocal-1.flac");
        await _library.VoiceProfileAsync();
        await _library.PendingUploadAsync();

        var job = await RunAsync(new BackupOptionsDto());

        job.Status.ShouldBe(LibraryJobStatusDto.Completed);
        var report = job.Report.ShouldBeOfType<BackupReportDto>();
        report.FileName.ShouldBe("stemstudio-backup-20261114-120000.zip");
        File.Exists(ArchivePath + ".partial").ShouldBeFalse();
        using var zip = ZipFile.OpenRead(ArchivePath);
        var names = zip.Entries.Select(entry => entry.FullName).ToList();
        names[0].ShouldBe("db/stemstudio.db");
        names[^1].ShouldBe("manifest.json");
        var files = _library.LibraryFiles().Keys.Select(path => "files/" + path).ToList();
        names.Where(name => name.StartsWith("files/", StringComparison.Ordinal)).ShouldBe(files, ignoreOrder: true);
        Schema.Validate(await ManifestAsync(zip)).ShouldBeEmpty();
        report.Included.Select(count => count.Category).ShouldContain(BackupCategoryDto.VoiceProfiles);
        report.Included.Single(count => count.Category == BackupCategoryDto.Stems).Files.ShouldBe(6);
        (report.Entries, report.Bytes).ShouldBe((names.Count - 2, new FileInfo(ArchivePath).Length));
        Gate.Mode.ShouldBe(LibraryGateMode.Open);
    }

    [Fact]
    public async Task CompactBackup_LeavesDetailStemsOutAndMarksThemRemovedInTheArchivedDatabase()
    {
        var track = await _library.TrackAsync();

        await RunAsync(new BackupOptionsDto { DetailStems = false });

        using var zip = ZipFile.OpenRead(ArchivePath);
        zip.Entries.ShouldNotContain(entry => entry.FullName.Contains("/detail/", StringComparison.Ordinal));
        var manifest = await ManifestAsync(zip);
        manifest.GetProperty("contents").GetProperty("excluded").EnumerateArray()
            .ShouldContain(e => e.GetProperty("category").GetString() == "detailStems" && e.GetProperty("reason").GetString() == "option");
        var archivedDb = Path.Combine(_factory.DataDir + "-backups", "extracted.db");
        zip.GetEntry("db/stemstudio.db")!.ExtractToFile(archivedDb);
        (await ScalarAsync(archivedDb, $"SELECT DetailPhase FROM Tracks WHERE Id = '{track.Id.ToString().ToUpperInvariant()}'"))!.ShouldContain("\"FilesRemovedAt\":\"2026-11-14T12:00:00Z\"");
    }

    [Fact]
    public async Task NotEnoughSpace_IsRefusedBeforeTheJob()
    {
        await _library.TrackAsync();
        _files.Free = 1000;

        var outcome = await StartAsync(new BackupOptionsDto());

        outcome.ProblemCode.ShouldBe(LibraryCareProblemTypes.InsufficientDiskSpace);
        var problem = await ProblemJsonAsync(outcome.Problem!);
        (problem.GetProperty("freeBytes").GetInt64(), problem.GetProperty("requiredBytes").GetInt64() > 64L * 1024 * 1024).ShouldBe((1000, true));
        Directory.Exists(_factory.BackupDir).ShouldBeTrue();
        Directory.GetFiles(_factory.BackupDir).ShouldBeEmpty();
    }

    [Fact]
    public async Task FreeSpaceFloor_StopsARunningBackupAndRemovesThePartialFile()
    {
        await _library.TrackAsync();
        _files.AfterFirstCheck = () => _files.Free = 1000;

        var job = await RunAsync(new BackupOptionsDto());

        (job.Status, job.Error!.Code).ShouldBe((LibraryJobStatusDto.Failed, LibraryJobErrorCodeDto.InsufficientDiskSpace));
        Directory.GetFiles(_factory.BackupDir).ShouldBeEmpty();
        Gate.Mode.ShouldBe(LibraryGateMode.Open);
        Directory.Exists(Path.Combine(_factory.DataDir, "library-care", "staging", job.JobId.ToString())).ShouldBeFalse();
    }

    [Fact]
    public async Task Cancel_EndsTheJobAndRemovesThePartialFile()
    {
        await _library.TrackAsync();
        var hold = new SemaphoreSlim(0);
        var reached = new SemaphoreSlim(0);
        _files.AfterFirstCheck = () =>
        {
            reached.Release();
            hold.Wait(TimeSpan.FromSeconds(10));
        };

        var outcome = await StartAsync(new BackupOptionsDto());
        await reached.WaitAsync(Ct);
        (await Runner.CancelAsync(outcome.Job!.JobId, Ct)).Result.ShouldBe(CancelResult.Requested);
        hold.Release();
        await Runner.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5), Ct);

        (await Runner.GetAsync(outcome.Job.JobId, Ct))!.Status.ShouldBe(LibraryJobStatusDto.Cancelled);
        Directory.GetFiles(_factory.BackupDir).ShouldBeEmpty();
        Gate.Mode.ShouldBe(LibraryGateMode.Open);
    }

    [Fact]
    public async Task MissingFile_IsAWarningAndNotAnEntry()
    {
        var track = await _library.TrackAsync();
        File.Delete(_library.PathOf($"tracks/{LibraryFixture.Id(track.Id)}/stems/bass.flac"));

        var job = await RunAsync(new BackupOptionsDto());

        var report = job.Report.ShouldBeOfType<BackupReportDto>();
        report.Warnings.ShouldContain(warning => warning.Code == ArchiveWarningCodeDto.MissingFile);
        using var zip = ZipFile.OpenRead(ArchivePath);
        zip.Entries.ShouldNotContain(entry => entry.FullName.EndsWith("/bass.flac", StringComparison.Ordinal));
        (await ManifestAsync(zip)).GetProperty("warnings").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task StoredClaudeKey_IsArchivedOnlyAsCiphertextAndTheKeyRingNever()
    {
        await _library.TrackAsync();
        using (var stored = await _client.PutAsJsonAsync(ApiRoutes.AgentClaudeKey, new { apiKey = ClaudeKeySentinels.Stored }, Ct))
        {
            stored.IsSuccessStatusCode.ShouldBeTrue();
        }

        await RunAsync(new BackupOptionsDto());

        var bytes = await File.ReadAllBytesAsync(ArchivePath, Ct);
        KeyLeaks.ShouldNotContainAny(Encoding.Latin1.GetString(bytes), "the archive", ClaudeKeySentinels.Stored);
        using var zip = ZipFile.OpenRead(ArchivePath);
        var db = Path.Combine(_factory.BackupDir, "secrets.db");
        zip.GetEntry("db/stemstudio.db")!.ExtractToFile(db);
        KeyLeaks.ShouldNotContainAny(Encoding.Latin1.GetString(await File.ReadAllBytesAsync(db, Ct)), "the archived database", ClaudeKeySentinels.Stored);
        (await ScalarAsync(db, "SELECT count(*) FROM AgentSecrets")).ShouldBe("1");
        zip.Entries.ShouldAllBe(entry => entry.FullName.StartsWith("files/", StringComparison.Ordinal) || entry.FullName == "db/stemstudio.db"
            || entry.FullName == "README.txt" || entry.FullName == "manifest.json");
        Directory.EnumerateFiles(_factory.KeyRingDir).Select(Path.GetFileName).ShouldAllBe(name => !zip.Entries.Any(entry => entry.Name == name));
    }

    [Fact]
    public async Task Logs_NeverContainTitlesOrPaths()
    {
        var track = await _library.TrackAsync();

        await RunAsync(new BackupOptionsDto());

        _factory.Logs.Entries.ShouldNotContain(entry => entry.Message.Contains(track.Title, StringComparison.Ordinal));
        _factory.Logs.Entries.ShouldNotContain(entry => entry.Message.Contains(_factory.DataDir, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnavailableFolder_IsRefused()
    {
        await File.WriteAllTextAsync(_factory.BackupDir, "not a folder", Ct);

        var outcome = await StartAsync(new BackupOptionsDto());

        outcome.ProblemCode.ShouldBe(LibraryCareProblemTypes.BackupsFolderUnavailable);
        File.Delete(_factory.BackupDir);
    }

    [Fact]
    public async Task SecondBackup_WhileOneRuns_IsRefused()
    {
        await _library.TrackAsync();
        var hold = new SemaphoreSlim(0);
        _files.AfterFirstCheck = () => hold.Wait(TimeSpan.FromSeconds(10));

        (await StartAsync(new BackupOptionsDto())).Job.ShouldNotBeNull();
        var second = await StartAsync(new BackupOptionsDto());
        hold.Release();
        await Runner.WhenIdleAsync();

        second.ProblemCode.ShouldBe(LibraryCareProblemTypes.LibraryJobRunning);
    }

    [Fact]
    public async Task NameClash_GetsASuffix()
    {
        await _library.TrackAsync();
        Directory.CreateDirectory(_factory.BackupDir);
        await File.WriteAllTextAsync(ArchivePath, "older", Ct);

        var job = await RunAsync(new BackupOptionsDto());

        job.Report.ShouldBeOfType<BackupReportDto>().FileName.ShouldBe("stemstudio-backup-20261114-120000-2.zip");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private LibraryGate Gate => _factory.Services.GetRequiredService<LibraryGate>();

    private LibraryJobRunner Runner => _factory.Services.GetRequiredService<LibraryJobRunner>();

    private Task<BackupStartOutcome> StartAsync(BackupOptionsDto options) =>
        _factory.WithScopeAsync(sp => sp.GetRequiredService<BackupService>().StartAsync(options, Ct));

    private async Task<LibraryJobDto> RunAsync(BackupOptionsDto options)
    {
        var outcome = await StartAsync(options);
        outcome.Job.ShouldNotBeNull(outcome.ProblemCode);
        await Runner.WhenIdleAsync();
        return (await Runner.GetAsync(outcome.Job.JobId, Ct))!;
    }

    private async Task<JsonElement> ProblemJsonAsync(IResult problem)
    {
        var http = new DefaultHttpContext { RequestServices = _factory.Services };
        using var body = new MemoryStream();
        http.Response.Body = body;
        await problem.ExecuteAsync(http);
        return JsonDocument.Parse(body.ToArray()).RootElement.Clone();
    }

    private static async Task<JsonElement> ManifestAsync(ZipArchive zip)
    {
        await using var stream = zip.GetEntry("manifest.json")!.Open();
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: Ct);
        return document.RootElement.Clone();
    }

    private static async Task<string?> ScalarAsync(string path, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>Free-space seam of the library-care tests.</summary>
internal sealed class FakeLibraryFiles : ILibraryFiles
{
    private int _checks;

    public long Free { get; set; } = 1L << 40;

    public long Total { get; set; } = 2L << 40;

    /// <summary>Runs once, after the first free-space check made while files are written.</summary>
    public Action? AfterFirstCheck { get; set; }

    /// <summary>Moves fail while this returns true for the source path.</summary>
    public Func<string, bool> FailMove { get; set; } = _ => false;

    public int MoveAttempts { get; private set; }

    public void Move(string source, string destination)
    {
        MoveAttempts++;
        if (FailMove(source))
        {
            throw new IOException("in use");
        }

        new LibraryFiles().Move(source, destination);
    }

    public VolumeSpace? Space(string path)
    {
        var space = new VolumeSpace(Free, Total);
        if (path.Contains("-backups", StringComparison.Ordinal) && Interlocked.Increment(ref _checks) == 2)
        {
            AfterFirstCheck?.Invoke();
        }

        return space;
    }
}
