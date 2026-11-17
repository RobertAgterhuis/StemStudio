using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts;
using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Components.Practice;

/// <summary>Code-behind of the section editor: works on a <see cref="SectionDraft"/> and saves it with <c>replaceSections</c>.</summary>
public sealed partial class SectionEditor
{
    /// <summary>Message of a 409 <c>sections-changed</c>.</summary>
    public const string ChangedMessage = "Another window saved the sections first. Cancel and open the editor again to see them.";

    private static readonly SectionKindDto[] Kinds = Enum.GetValues<SectionKindDto>();

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>Track.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>The stored set the draft starts from.</summary>
    [Parameter, EditorRequired]
    public SectionSetDto Set { get; set; } = default!;

    /// <summary>Song length.</summary>
    [Parameter]
    public double Duration { get; set; }

    /// <summary>Beat grid for snapping.</summary>
    [Parameter]
    public BeatGridDto? BeatGrid { get; set; }

    /// <summary>Playhead (split).</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>Channels a solo can name.</summary>
    [Parameter]
    public IReadOnlyList<string> Channels { get; set; } = [];

    /// <summary>Raised with the stored set after Done.</summary>
    [Parameter]
    public EventCallback<SectionSetDto> OnSaved { get; set; }

    /// <summary>Raised by Cancel.</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    private SectionDraft Draft { get; set; } = default!;

    private string? Error { get; set; }

    private bool Saving { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized() => Draft = new SectionDraft(Set, Duration, BeatGrid);

    private static string WireName(SectionKindDto kind) => SectionText.WireKind(kind);

    private static SectionKindDto ParseKind(object? value) =>
        Kinds.FirstOrDefault(k => SectionText.WireKind(k) == value?.ToString(), SectionKindDto.Other);

    private void Change(Action edit)
    {
        edit();
        Error = null;
    }

    private void Split()
    {
        Error = Draft.Split(Position) ? null : "Move the playhead at least one second into a section to split it.";
    }

    private void MoveStart(int index, object? value)
    {
        if (double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            Draft.MoveStart(index, seconds);
        }
    }

    private async Task SaveAsync()
    {
        if (Draft.Problem() is { } problem)
        {
            Error = problem;
            return;
        }

        Saving = true;
        try
        {
            await OnSaved.InvokeAsync(await Api.ReplaceSectionsAsync(TrackId, Draft.ToRequest()));
        }
        catch (ApiException ex)
        {
            Error = ex.Error.IsConflict && ex.Error.Code == PracticeFlowProblemTypes.SectionsChanged ? ChangedMessage : ex.Error.UserMessage;
        }
        finally
        {
            Saving = false;
        }
    }
}
