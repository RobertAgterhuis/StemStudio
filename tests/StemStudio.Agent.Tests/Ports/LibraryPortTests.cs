using StemStudio.Agent.Ports;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;

namespace StemStudio.Agent.Tests.Ports;

/// <summary>Skeleton tests (architect, 022 Phase 0 T003): the library port records of tool catalogue 1.1.0.</summary>
public sealed class LibraryPortTests
{
    private static readonly Guid Id = Guid.Parse("4b6d8f0a-1c3e-4a5b-9d7f-2e4c6a8b0d1f");

    [Fact]
    public void LibraryInfoTarget_NamesTheScopeAndTheId()
    {
        (LibraryInfoTarget.ForTrack(Id), LibraryInfoTarget.ForItem(Id)).ShouldBe((new LibraryInfoTarget(LibraryInfoScope.Track, Id), new LibraryInfoTarget(LibraryInfoScope.Item, Id)));
    }

    [Fact]
    public void LibraryFolderQuery_DefaultsToTheRootWithTheLevelDefaults()
    {
        var root = new LibraryFolderQuery();
        var folder = root with { FolderId = "ar0123456789abcdef", Sort = FolderSortDto.Year, Kind = AssetKindDto.Audio, Limit = 20 };

        (root.FolderId, root.Sort, root.Kind, root.Limit).ShouldBe((null, null, null, null));
        (folder.FolderId, folder.Sort, folder.Kind, folder.Limit).ShouldBe(("ar0123456789abcdef", FolderSortDto.Year, AssetKindDto.Audio, 20));
    }

    [Fact]
    public void LibrarySearchQuery_SearchesTheWholeLibraryByDefault()
    {
        var query = new LibrarySearchQuery { Query = "snare" };

        (query.Query, query.Within, query.Limit).ShouldBe(("snare", null, null));
        (query with { Within = "so0123456789abcdef", Limit = 10 }).Within.ShouldBe("so0123456789abcdef");
    }

    [Fact]
    public void StemStudioActions_HaveALibraryGroup()
    {
        typeof(IStemStudioActions).GetProperty(nameof(IStemStudioActions.Library))!.PropertyType.ShouldBe(typeof(ILibraryActions));
        typeof(ILibraryActions).GetMethods().Select(method => method.Name)
            .ShouldBe(["BrowseLibraryFolderAsync", "SearchLibraryAsync", "GetLibraryInfoAsync", "SetLibraryInfoAsync"], ignoreOrder: true);
    }
}
