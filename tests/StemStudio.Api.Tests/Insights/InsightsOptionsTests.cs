using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StemStudio.Api.Insights;
using StemStudio.Api.Tests.TestSupport;

namespace StemStudio.Api.Tests.Insights;

/// <summary>
/// <c>StemStudio:MaxSetlistHandoutSongs</c> may lower the setlist handout limit but never raise it above the worker contract's
/// 50 songs (worker 1.11.0 <c>HandoutDocument.songs</c> maxItems).
/// </summary>
public sealed class InsightsOptionsTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("51")]
    public void ASongLimitOutsideOneToFifty_StopsTheStart(string value)
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:MaxSetlistHandoutSongs"] = value } };

        var error = Should.Throw<OptionsValidationException>(() => factory.CreateClient().Dispose());

        error.Message.ShouldContain("StemStudio:MaxSetlistHandoutSongs");
    }

    [Fact]
    public void ALowerSongLimit_IsAccepted()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:MaxSetlistHandoutSongs"] = "20" } };
        using var client = factory.CreateClient();

        factory.Services.GetRequiredService<IOptions<InsightsOptions>>().Value.MaxSetlistHandoutSongs.ShouldBe(20);
    }
}
