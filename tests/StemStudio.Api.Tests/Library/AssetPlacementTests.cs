using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Assets;
using StemStudio.Api.Library;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T034: <c>Asset.placement</c> on every asset response; the preview before the backfill (data-model 5.4).</summary>
public sealed class AssetPlacementTests : ApiTestBase
{
    [Fact]
    public async Task UnfiledItem_GetsAnAutomaticPreview()
    {
        var item = Asset.Create(Guid.NewGuid(), "drums", ExportProvenance(Guid.NewGuid()), T0);
        await Factory.AddUnfiledAsync(item);

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Asset(item.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getAsset");
        var placement = (await response.BodyAsync<AssetDto>()).Placement!;
        (placement.Filed, placement.Artist.Name, placement.Song.FolderId).ShouldBe((false, "Rick Astley", "soc02cef10277ea444"));
    }

    [Fact]
    public async Task FiledItem_ListAndRename_CarryThePlacement()
    {
        var item = Asset.Create(Guid.NewGuid(), "drums", ExportProvenance(Guid.NewGuid()), T0);
        await Factory.FileAsync(item);

        using var list = await Client.GetAsync(new Uri(ApiRoutes.Assets, UriKind.Relative), Ct);
        using var rename = await Client.PatchAsJsonAsync(new Uri(ApiRoutes.Asset(item.Id), UriKind.Relative), new RenameAssetRequest("Renamed"), Ct);

        await ContractAssert.MatchesAsync(list, "listAssets");
        await ContractAssert.MatchesAsync(rename, "renameAsset");
        (await list.BodyAsync<AssetListDto>()).Items.Single().Placement!.Filed.ShouldBeTrue();
        (await rename.BodyAsync<AssetDto>()).Placement!.Song.Name.ShouldBe("Never Gonna Give You Up");
    }

    [Fact]
    public void Mapper_NeverFiledItems_HaveNoPlacement()
    {
        var item = Asset.Create(Guid.NewGuid(), "x", ExportProvenance(null) with { Origin = (AssetOrigin)99 }, T0);

        LibraryMapper.PlacementOf(item).ShouldBeNull();
        AssetMapper.ToDto(item, trackAvailable: false).Placement.ShouldBeNull();
    }

    [Fact]
    public void Mapper_EnumsMatchTheContractByName()
    {
        foreach (var source in Enum.GetValues<PlacementSource>())
        {
            LibraryMapper.ToDto(source).ToString().ShouldBe(source.ToString());
        }

        foreach (var level in Enum.GetValues<FolderLevel>())
        {
            LibraryMapper.ToDto(level).ToString().ShouldBe(level.ToString());
        }

        Enum.GetValues<PlacementSourceDto>().Length.ShouldBe(Enum.GetValues<PlacementSource>().Length);
        Enum.GetValues<FolderLevelDto>().Length.ShouldBe(Enum.GetValues<FolderLevel>().Length);
    }

    [Fact]
    public void Mapper_RejectsNull()
    {
        Should.Throw<ArgumentNullException>(() => LibraryMapper.PlacementOf(null!));
        Should.Throw<ArgumentNullException>(() => LibraryMapper.ToDto((Placement)null!, filed: true));
    }

    [Fact]
    public async Task Services_ResolveTheFilerAndTheTrigger()
    {
        await Factory.WithScopeAsync(sp =>
        {
            sp.GetRequiredService<ILibraryFiler>().ShouldBeOfType<LibraryFiler>();
            sp.GetRequiredService<ILibraryFilingTrigger>().ShouldBeSameAs(sp.GetRequiredService<LibraryFilingBackfill>());
            return Task.FromResult(true);
        });
    }
}
