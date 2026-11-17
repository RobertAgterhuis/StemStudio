using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Assets;
using StemStudio.Api.Recording;
using StemStudio.Api.Tracks;

namespace StemStudio.Api.Tests.Recording;

/// <summary>
/// Feature 015 T007: Assets and Tracks only know their own seams; the recording module implements them and
/// <see cref="RecordingRegistration.AddRecording"/> wires them.
/// </summary>
public sealed class RecordingSeamWiringTests : RecordingTestBase
{
    [Fact]
    public async Task AddRecording_ResolvesTheAssetAndTrackSeams_ToTheRecordingImplementations()
    {
        var types = await Factory.WithScopeAsync(sp => Task.FromResult((
            Finisher: sp.GetRequiredService<IAssetFinisher>().GetType(),
            Export: sp.GetRequiredService<ITakeExportSource>().GetType(),
            Cleanup: sp.GetRequiredService<ITrackTakesCleanup>().GetType())));

        types.ShouldBe((typeof(TakeAssetFinisher), typeof(TakeExportResolver), typeof(TakeDeletion)));
    }

    [Fact]
    public void AddRecording_RegistersEachSeamOnce_AsScoped()
    {
        var services = new ServiceCollection().AddRecording(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        foreach (var seam in new[] { typeof(IAssetFinisher), typeof(ITakeExportSource), typeof(ITrackTakesCleanup) })
        {
            services.Where(descriptor => descriptor.ServiceType == seam).ShouldHaveSingleItem().Lifetime.ShouldBe(ServiceLifetime.Scoped);
        }
    }
}
