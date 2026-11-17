using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Sections;
using StemStudio.Web.Api;
using StemStudio.Web.Practice;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Practice;

/// <summary>
/// The section lane (014 US1). Loads the section set of the track, starts detection once for tracks without sections
/// (status <c>none</c>, FR-001), polls while detecting and reports every new set through <see cref="OnLoaded"/>; the
/// mixer keeps the set in <c>MixerState</c> and passes it back as <see cref="Set"/>.
/// </summary>
public sealed partial class SectionLane
{
    private bool _detectRequested;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>Track whose sections are shown.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>The current section set (from the mixer state).</summary>
    [Parameter]
    public SectionSetDto? Set { get; set; }

    /// <summary>Playback position (marks the current section).</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>The loop region (marks the looped section).</summary>
    [Parameter]
    public LoopRegionDto? Loop { get; set; }

    /// <summary>Raised with every loaded or polled set.</summary>
    [Parameter]
    public EventCallback<SectionSetDto> OnLoaded { get; set; }

    /// <summary>Raised when a section button is pressed (loop that section).</summary>
    [Parameter]
    public EventCallback<SectionDto> OnLoop { get; set; }

    /// <summary>Raised with a mixer command id by the navigation buttons (the same commands as the keys).</summary>
    [Parameter]
    public EventCallback<string> OnCommand { get; set; }

    /// <summary>Extra actions after the navigation buttons (editor, re-detect).</summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>Extra content per section (Expert confidence).</summary>
    [Parameter]
    public RenderFragment<SectionDto>? ItemExtra { get; set; }

    private IReadOnlyList<SectionDto> Sections => Set?.Sections ?? [];

    private SectionDto? Current => SectionNavigator.Current(Sections, Position);

    private bool NoSections => Sections.Count == 0;

    private bool Failed => Set?.Status == SectionSetStatusDto.Failed;

    private string? StatusText => SectionText.Status(Set);

    private string WireStatus => Set is null ? "none" : WireName.Of(Set.Status);

    private string? Error { get; set; }

    /// <inheritdoc />
    protected override bool NeedsPolling => Set?.Status is SectionSetStatusDto.Queued or SectionSetStatusDto.Detecting
        || (Set?.Status == SectionSetStatusDto.None && _detectRequested);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadAsync(Api.GetSectionsAsync(TrackId, Stopping));
        if (Set?.Status == SectionSetStatusDto.None)
        {
            await DetectAsync();
        }

        StartPolling();
    }

    /// <inheritdoc />
    protected override Task RefreshAsync(CancellationToken ct) => LoadAsync(Api.GetSectionsAsync(TrackId, ct));

    private static string Bool(bool value) => value ? "true" : "false";

    private static string KindText(SectionDto section) =>
        section.Instrument is { } instrument && section.Kind == SectionKindDto.Solo ? $"solo, {instrument}" : SectionText.Kind(section.Kind);

    private static string ButtonClass(SectionDto section, bool current, bool looped) =>
        $"ss-section ss-section-{SectionText.WireKind(section.Kind)}{(current ? " ss-section-current" : null)}{(looped ? " ss-section-looped" : null)}";

    private async Task RetryAsync()
    {
        await DetectAsync();
        StartPolling();
    }

    private async Task DetectAsync()
    {
        _detectRequested = true;
        await LoadAsync(Api.DetectSectionsAsync(TrackId, overwrite: false, Stopping));
    }

    private async Task LoadAsync(Task<SectionSetDto> request)
    {
        try
        {
            var set = await request;
            Error = null;
            await OnLoaded.InvokeAsync(set);
            Set = set;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }
}
