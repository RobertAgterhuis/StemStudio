using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Library;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>
/// The library page (feature 022) on the contract examples: the root answers <c>RootView</c>, every folder answers
/// what <see cref="FolderAnswers"/> holds, the search answers <c>EmptySearch</c> unless a test says otherwise.
/// </summary>
public abstract class LibraryTestContext : PageTestContext
{
    protected LibraryTestContext()
    {
        Services.AddLibraryUi();
        Api.GetLibraryRootAsync(Arg.Any<LibraryFolderRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(_ => RootAnswer());
        Api.GetLibraryFolderAsync(Arg.Any<string>(), Arg.Any<LibraryFolderRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => FolderAnswer(call.ArgAt<string>(0)));
        Api.SearchLibraryAsync(Arg.Any<LibrarySearchRequest>(), Arg.Any<CancellationToken>()).Returns(_ => SearchAnswer());
    }

    /// <summary>The root answers, in order; the last one repeats.</summary>
    protected Queue<Func<LibraryFolderViewDto?>> RootAnswers { get; } = new();

    /// <summary>The answers per folder id, in order; the last one repeats.</summary>
    protected Dictionary<string, Queue<Func<LibraryFolderViewDto?>>> FolderAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>The search answer.</summary>
    protected Func<LibrarySearchResultDto> SearchAnswer { get; set; } = LibraryData.EmptySearch;

    protected BunitNavigationManager Navigation => (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

    protected void Folder(string id, params Func<LibraryFolderViewDto?>[] answers) =>
        FolderAnswers[id] = new Queue<Func<LibraryFolderViewDto?>>(answers);

    protected void Folder(string id, LibraryFolderViewDto view) => Folder(id, () => view);

    protected IRenderedComponent<CascadingValue<ExperienceContext>> RenderLibrary(
        string url = "library", ExperienceLevel level = ExperienceLevel.Advanced, string? folderId = null)
    {
        Navigation.NavigateTo(url);
        return RenderAtLevel<LibraryPage>(level, p => p.Add(x => x.FolderId, folderId));
    }

    protected IRenderedComponent<CascadingValue<ExperienceContext>> RenderFolder(string folderId, ExperienceLevel level = ExperienceLevel.Advanced) =>
        RenderLibrary($"library/{folderId}", level, folderId);

    protected IEnumerable<LibraryFolderRequest> FolderRequests(string id) =>
        Api.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IStemStudioApiClient.GetLibraryFolderAsync) && (string)c.GetArguments()[0]! == id)
            .Select(c => (LibraryFolderRequest)c.GetArguments()[1]!);

    protected IEnumerable<string?> KnownRevisions(string? folderId = null) =>
        Api.ReceivedCalls().Where(c => folderId is null
                ? c.GetMethodInfo().Name == nameof(IStemStudioApiClient.GetLibraryRootAsync)
                : c.GetMethodInfo().Name == nameof(IStemStudioApiClient.GetLibraryFolderAsync) && (string)c.GetArguments()[0]! == folderId)
            .Select(c => (string?)c.GetArguments()[folderId is null ? 1 : 2]);

    private static LibraryFolderViewDto? Next(Queue<Func<LibraryFolderViewDto?>> answers) =>
        answers.Count > 1 ? answers.Dequeue()() : answers.Peek()();

    private LibraryFolderViewDto? RootAnswer() => RootAnswers.Count == 0 ? LibraryData.RootView() : Next(RootAnswers);

    private LibraryFolderViewDto? FolderAnswer(string id) =>
        FolderAnswers.TryGetValue(id, out var answers) ? Next(answers) : throw Problem(System.Net.HttpStatusCode.NotFound, "This folder is empty now");
}
