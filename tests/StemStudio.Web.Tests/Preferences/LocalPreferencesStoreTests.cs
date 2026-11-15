using System.Text.Json;
using Microsoft.JSInterop;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Preferences;

public sealed class LocalPreferencesStoreTests
{
    private readonly FakeWebInterop _interop = new();
    private readonly LocalPreferencesStore _store;

    public LocalPreferencesStoreTests() => _store = new LocalPreferencesStore(_interop);

    [Fact]
    public async Task Load_MapsTheValidatedRecord_AndStorageFlag()
    {
        _interop.ReadResult = new StoredPreferencesRead(
            JsonDocument.Parse("""{"schemaVersion":1,"level":"advanced","theme":"light"}""").RootElement, false);

        var snapshot = await _store.LoadAsync(TestContext.Current.CancellationToken);

        snapshot.Preferences.Level.ShouldBe(ExperienceLevel.Advanced);
        snapshot.Preferences.Theme.ShouldBe(ThemePreference.Light);
        snapshot.StorageAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Load_WhenTheModuleFails_GivesDefaults_AndUnavailableStorage()
    {
        _interop.Failure = new JSException("module not found");

        var snapshot = await _store.LoadAsync(TestContext.Current.CancellationToken);

        snapshot.ShouldBe(new PreferencesSnapshot(UserPreferences.Default, false));
    }

    [Fact]
    public async Task Save_WritesTheRecordJson_AndReturnsTheResult()
    {
        _interop.WriteResult = false;

        var ok = await _store.SaveAsync(UserPreferences.Default with { Level = ExperienceLevel.Expert }, TestContext.Current.CancellationToken);

        ok.ShouldBeFalse();
        _interop.Written.Single().GetProperty("level").GetString().ShouldBe("expert");
    }

    [Fact]
    public async Task Save_WhenTheCircuitIsGone_ReturnsFalse()
    {
        _interop.Failure = new JSDisconnectedException("gone");

        var ok = await _store.SaveAsync(UserPreferences.Default, TestContext.Current.CancellationToken);

        ok.ShouldBeFalse();
    }
}
