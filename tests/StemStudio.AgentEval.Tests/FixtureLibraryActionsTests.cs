using StemStudio.Agent.Ports;
using StemStudio.AgentEval.Fixture;
using StemStudio.Contracts.Library;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.AgentEval.Tests;

/// <summary>022 T083 / 023 T055: the library folders and the lyric sheets of the evaluation fixture.</summary>
public sealed class FixtureLibraryActionsTests
{
    private readonly FixtureLibrary _library = FixtureLibrary.Seed(Repo.Presets);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void FixtureActions_ExposeTheLibraryGroup()
    {
        new FixtureActions(_library).Library.ShouldBeOfType<FixtureLibraryActions>();
    }

    [Fact]
    public async Task TheRoot_ListsTheArtists_AndAFolderItsChildrenAndItems()
    {
        var actions = new FixtureLibraryActions(_library);

        var root = await actions.BrowseLibraryFolderAsync(new LibraryFolderQuery(), Token);
        var song = await actions.BrowseLibraryFolderAsync(new LibraryFolderQuery { FolderId = _library.Ids.Folder("so-ace") }, Token);

        root.Folder.ShouldBeNull();
        root.Children.Select(folder => folder.Name).ShouldBe(["Metallica", "Motörhead", "Unknown artist"]);
        song.Folder!.Name.ShouldBe("Ace of Spades");
        song.Breadcrumb.Select(item => item.Level).ShouldBe([FolderLevelDto.Artist, FolderLevelDto.Album, FolderLevelDto.Song]);
        song.Items.Count.ShouldBe(2);
        _library.Ids.Folder("so-ace").ShouldMatch(StemStudio.Contracts.ContractLimits.FolderIdPattern);
        _library.Ids.ToKeys($"\"{_library.Ids.Folder("so-ace")}\"").ShouldBe("\"so-ace\"");
        _library.Ids.ToIds("\"so-ace\"").ShouldBe($"\"{_library.Ids.Folder("so-ace")}\"");
        await Should.ThrowAsync<AgentActionException>(() => actions.BrowseLibraryFolderAsync(new LibraryFolderQuery { FolderId = "ar0000000000000000" }, Token));
    }

    [Fact]
    public async Task Search_FindsInstrumentWordsAndNames_WithinAFolder()
    {
        var actions = new FixtureLibraryActions(_library);

        var snare = await actions.SearchLibraryAsync(new LibrarySearchQuery { Query = "snare" }, Token);
        var ace = await actions.SearchLibraryAsync(new LibrarySearchQuery { Query = "ace of spades" }, Token);
        var bass = await actions.SearchLibraryAsync(new LibrarySearchQuery { Query = "bass", Within = _library.Ids.Folder("ar-unknown") }, Token);

        snare.Items.Single().MatchedInstruments.ShouldBe(["drums.snare"]);
        ace.Albums.Count.ShouldBe(1);
        ace.Songs.Count.ShouldBe(1);
        ace.Totals.Items.ShouldBe(2);
        bass.Items.Single().Asset.Name.ShouldBe("Untitled jam - bass");
    }

    [Fact]
    public async Task LibraryInfo_IsKeptPerTarget()
    {
        var actions = new FixtureLibraryActions(_library);
        var target = LibraryInfoTarget.ForItem(_library.Ids.Of("a-ace-snare"));

        (await actions.GetLibraryInfoAsync(target, Token)).Album.Value.ShouldBeNull();
        await actions.SetLibraryInfoAsync(target, new LibraryInfoRequest { Artist = null, Album = "Ace of Spades", Song = null }, Token);

        (await actions.GetLibraryInfoAsync(target, Token)).Album.Value.ShouldBe("Ace of Spades");
        actions.Changes.Count.ShouldBe(1);
    }

    [Fact]
    public async Task LyricSheets_HaveOpenSuggestions_AndTheFakeSongIsRefused()
    {
        var lyrics = new FixtureLyricAssistActions(_library);
        var zomer = _library.Ids.Of("l-zomer");

        var state = await lyrics.GetSheetStateAsync(zomer, Token);
        var spelling = await lyrics.ListSuggestionsAsync(zomer, SuggestionStatusDto.Open, SuggestionCategoryDto.Spelling, Token);
        var accepted = await lyrics.AcceptAsync(zomer, new SuggestionDecisionRequest { SuggestionIds = [.. spelling.Items.Select(item => item.SuggestionId)], ExpectedRevision = state.Revision }, Token);
        var fake = await Should.ThrowAsync<AgentActionException>(() => lyrics.StartAssistAsync(_library.Ids.Of("l-fake-song"), new CreateLyricAssistRequest { Kind = AssistKindDto.Check, ExpectedRevision = 3 }, Token));
        var assist = await lyrics.StartAssistAsync(zomer, new CreateLyricAssistRequest { Kind = AssistKindDto.Check, ExpectedRevision = 4 }, Token);

        spelling.Items.Count.ShouldBe(2);
        (accepted.Revision, accepted.Applied).ShouldBe((4, 2));
        fake.ProblemCode.ShouldBe(LyricAssistProblemTypes.NotOwnLyrics);
        (await lyrics.GetAssistAsync(assist.AssistId, Token)).Status.ShouldBe(AssistStatusDto.Completed);
        (await lyrics.ListSuggestionsAsync(_library.Ids.Of("l-inject"), null, null, Token)).Items.ShouldBeEmpty();
        await Should.ThrowAsync<AgentActionException>(() => lyrics.GetSheetStateAsync(Guid.NewGuid(), Token));
        await Should.ThrowAsync<AgentActionException>(() => lyrics.StartAssistAsync(zomer, new CreateLyricAssistRequest { Kind = AssistKindDto.Check, ExpectedRevision = 1 }, Token));
        (await lyrics.FindRhymesAsync("night", AssistLanguageDto.En, 10, Token)).Rhymes.ShouldNotBeEmpty();
        (await lyrics.CountSyllablesAsync(zomer, Token)).Lines.Count.ShouldBe(2);
        (await lyrics.UndoAsync(zomer, 4, Token)).Revision.ShouldBe(5);
        (await lyrics.MarkIntentionalAsync(zomer, Guid.NewGuid(), new MarkSuggestionIntentionalRequest { Scope = IntentionalScopeDto.Word, ExpectedRevision = 5 }, Token)).Scope.ShouldBe(IntentionalScopeDto.Word);
        await lyrics.DeleteIntentionalMarkAsync(zomer, Guid.NewGuid(), Token);
    }
}
