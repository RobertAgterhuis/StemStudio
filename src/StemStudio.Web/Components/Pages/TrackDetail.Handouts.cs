using StemStudio.Contracts.Handouts;
using StemStudio.Web.Components.Handouts;
using StemStudio.Web.Handouts;

namespace StemStudio.Web.Components.Pages;

/// <summary>
/// Lesson handouts on the track page (020 US3): the menu, the practice notes, and the create links of missing
/// ingredients, which go to existing flows only (Q3): the sheet music dialog, the lyrics and detail cards, the mixer or
/// the notes editor.
/// </summary>
public sealed partial class TrackDetail
{
    private PracticeNotesEditor? _notes;

    private HandoutSourceDto HandoutSource => HandoutSourceDto.Song(Id);

    private Task OnIngredientAsync(IngredientRequest request)
    {
        var target = IngredientLinks.Target(Id, request.Action, request.Family);
        if (target.Notes)
        {
            return _notes!.FocusAsync(request.Family);
        }

        if (target.ScoreStem is { } stem)
        {
            OpenScore(stem);
            return Task.CompletedTask;
        }

        Navigation.NavigateTo(target.Url!);
        return Task.CompletedTask;
    }
}
