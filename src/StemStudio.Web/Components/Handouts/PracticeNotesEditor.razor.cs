using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts;
using StemStudio.Contracts.Handouts;
using StemStudio.Web.Api;
using StemStudio.Web.Handouts;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Handouts;

/// <summary>
/// The practice notes editor: loads <c>getPracticeNotes</c> once, keeps one draft per family (switching keeps unsaved
/// text) and saves the shown family with <c>setPracticeNote</c> (an empty text removes the note).
/// </summary>
public sealed partial class PracticeNotesEditor
{
    private readonly Dictionary<HandoutFamilyDto, string> _drafts = [];
    private ElementReference _text;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>The track.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    private HandoutFamilyDto Family { get; set; } = HandoutFamilyDto.Overview;

    private string Text => _drafts.GetValueOrDefault(Family, string.Empty);

    private string Counter => string.Create(
        CultureInfo.InvariantCulture, $"{Text.Length:N0} / {ContractLimits.MaxPracticeNoteLength:N0} characters");

    private bool Saving { get; set; }

    private string Status { get; set; } = string.Empty;

    /// <summary>Shows the note of a family and focuses the text (the "Write practice notes" link of the handout dialog).</summary>
    /// <param name="family">The family.</param>
    /// <returns>A task.</returns>
    public async Task FocusAsync(HandoutFamilyDto family)
    {
        Family = family;
        StateHasChanged();
        await FocusOnce.TryAsync(_text);
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var notes = await Api.GetPracticeNotesAsync(TrackId);
            foreach (var note in notes.Notes)
            {
                _drafts[note.Family] = note.Text;
            }
        }
        catch (ApiException ex)
        {
            Status = ex.Error.UserMessage;
        }
    }

    private void OnFamily(ChangeEventArgs args) => Family = HandoutText.Families.First(f => HandoutText.Wire(f) == args.Value?.ToString());

    private void SetText(string? value) => _drafts[Family] = value ?? string.Empty;

    private async Task SaveAsync()
    {
        var family = Family;
        Saving = true;
        var (stored, error) = await ApiCall.TryAsync(async () => new Saved(await Api.SetPracticeNoteAsync(TrackId, family, Text)));
        Saving = false;
        if (stored is { Note: var note })
        {
            _drafts[family] = note?.Text ?? string.Empty;
        }

        Status = error ?? $"{(stored!.Note is null ? "Removed" : "Saved")} the note for {HandoutText.Family(family)}.";
    }

    // Wraps the nullable result (204 = removed) so that a failed call and a removal differ.
    private sealed record Saved(PracticeNoteDto? Note);
}
