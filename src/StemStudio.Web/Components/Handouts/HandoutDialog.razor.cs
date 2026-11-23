using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Handouts;
using StemStudio.Web.Api;
using StemStudio.Web.Handouts;

namespace StemStudio.Web.Components.Handouts;

/// <summary>
/// The handout dialog: loads <c>getHandoutOptions</c> for the chosen family (and transposition), keeps the choices in
/// <see cref="HandoutChoices"/> and posts them with <c>createHandout</c>.
/// </summary>
public sealed partial class HandoutDialog
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>Song or setlist.</summary>
    [Parameter, EditorRequired]
    public HandoutSourceDto Source { get; set; } = default!;

    /// <summary>The family the dialog opens with.</summary>
    [Parameter]
    public HandoutFamilyDto Family { get; set; } = HandoutFamilyDto.Overview;

    /// <summary>Raised with the created item.</summary>
    [Parameter]
    public EventCallback<HandoutCreated> OnCreated { get; set; }

    /// <summary>Raised on cancel, Escape and before a create link is followed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>A create link was chosen; unbound hides the links (setlist handouts).</summary>
    [Parameter]
    public EventCallback<IngredientRequest> OnIngredientAction { get; set; }

    private HandoutChoices Choices { get; } = new();

    private HandoutFamilyDto Chosen { get; set; }

    private bool Busy { get; set; }

    private string? Error { get; set; }

    private EventCallback<IngredientActionDto>? ActionHandler =>
        OnIngredientAction.HasDelegate ? EventCallback.Factory.Create<IngredientActionDto>(this, FollowAsync) : null;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        Chosen = Family;
        await LoadAsync(first: true);
    }

    private async Task LoadAsync(bool first = false)
    {
        try
        {
            var options = await Api.GetHandoutOptionsAsync(Source, Chosen, Chosen == HandoutFamilyDto.Melodic ? Choices.Transposition : null);
            Choices.Reset(options, first);
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private Task OnFamilyAsync(ChangeEventArgs args)
    {
        Chosen = HandoutText.Families.First(f => HandoutText.Wire(f) == args.Value?.ToString());
        Choices.Transposition = null;
        return LoadAsync();
    }

    private Task OnTranspositionAsync(ChangeEventArgs args)
    {
        Choices.Transposition = Enum.GetValues<TranspositionDto>().First(t => StemStudio.Web.Services.WireName.Of(t) == args.Value?.ToString());
        return LoadAsync();
    }

    private void OnPaper(ChangeEventArgs args) => Choices.Paper = args.Value?.ToString() == "letter" ? PaperDto.Letter : PaperDto.A4;

    private async Task SubmitAsync()
    {
        Busy = true;
        var (asset, error) = await ApiCall.TryAsync(() => Api.CreateHandoutAsync(Choices.Request(Source, Chosen)));
        (Busy, Error) = (false, error ?? Error);
        await (asset is null ? Task.CompletedTask : OnCreated.InvokeAsync(new HandoutCreated(asset, Chosen)));
    }

    private async Task FollowAsync(IngredientActionDto action)
    {
        await OnClose.InvokeAsync();
        await OnIngredientAction.InvokeAsync(new IngredientRequest(action, Chosen));
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnClose);
}
