using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Generation;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;
using StemStudio.Web.Generation;

namespace StemStudio.Web.Components.Generation;

/// <summary>Loads the generations of a <see cref="GenerationQuery"/>, polls while one is active and runs the row actions.</summary>
public sealed partial class GenerationList
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private AgentRunNotifier AgentRuns { get; set; } = default!;

    /// <summary>The filters (all generations, one kind, or the restyles of one track).</summary>
    [Parameter, EditorRequired]
    public GenerationQuery Query { get; set; } = default!;

    /// <summary>Text of the empty state.</summary>
    [Parameter]
    public string EmptyText { get; set; } = "Nothing generated yet.";

    private IReadOnlyList<GenerationDto>? Items { get; set; }

    private string? Error { get; set; }

    private string? ActionError { get; set; }

    private GenerationDto? Deleting { get; set; }

    /// <summary>The song take of the open "Use my voice" dialog.</summary>
    private TakeRef? Converting { get; set; }

    /// <inheritdoc />
    protected override bool NeedsPolling => Items?.Any(g => GenerationText.IsActive(g.Status)) == true;

    /// <summary>Shows a generation that was just started on top of the list and polls it.</summary>
    /// <param name="generation">The new generation.</param>
    /// <returns>A task that completes when the list is re-rendered.</returns>
    public async Task AddAsync(GenerationDto generation)
    {
        Items = [generation, .. (Items ?? []).Where(g => g.Id != generation.Id)];
        StartPolling();
        await InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        AgentRuns.RunEnded += OnAgentRunEnded;
        await RefreshAsync(Stopping);
        StartPolling();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        AgentRuns.RunEnded -= OnAgentRunEnded;
        base.Dispose(disposing);
    }

    /// <summary>An agent run may have started or removed generations: reload (011 US6).</summary>
    private void OnAgentRunEnded() => _ = InvokeAsync(async () =>
    {
        await RefreshAsync(Stopping);
        StartPolling();
        StateHasChanged();
    });

    /// <inheritdoc />
    protected override async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            (Items, Error) = (await Api.ListGenerationsAsync(Query, ct), null);
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private Task CancelAsync(GenerationDto generation) => RunAsync(async () => Replace(await Api.CancelGenerationAsync(generation.Id, Stopping)));

    private Task RetryAsync(GenerationDto generation) => RunAsync(async () => await AddAsync(await Api.RetryGenerationAsync(generation.Id, Stopping)));

    private void AskDelete(GenerationDto generation) => Deleting = generation;

    private async Task OnConversionStartedAsync(GenerationDto conversion)
    {
        Converting = null;
        await AddAsync(conversion);
    }

    /// <summary>A take of a generation.</summary>
    /// <param name="Generation">The generation.</param>
    /// <param name="Take">The take.</param>
    private sealed record TakeRef(GenerationDto Generation, GenerationTakeDto Take);

    private Task ImportAsync(GenerationDto generation, GenerationTakeDto take) => RunAsync(async () =>
    {
        var track = await Api.ImportTakeAsync(generation.Id, take.Index, Stopping);
        Navigation.NavigateTo($"tracks/{track.Id}");
    });

    private Task DeleteAsync() => RunAsync(async () =>
    {
        var id = Deleting!.Id;
        Deleting = null;
        await Api.DeleteGenerationAsync(id, Stopping);
        Items = [.. Items!.Where(g => g.Id != id)];
    });

    private void Replace(GenerationDto generation) => Items = [.. Items!.Select(g => g.Id == generation.Id ? generation : g)];

    private async Task RunAsync(Func<Task> action) => ActionError = await ApiCall.RunAsync(action);
}
