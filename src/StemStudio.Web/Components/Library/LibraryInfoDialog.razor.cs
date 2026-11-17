using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Library;
using StemStudio.Web.Api;
using StemStudio.Web.Library;

namespace StemStudio.Web.Components.Library;

/// <summary>Loads, validates and saves the library info of a track or an item and reports where the item went.</summary>
public sealed partial class LibraryInfoDialog
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>What is edited.</summary>
    [Parameter, EditorRequired]
    public LibraryInfoTarget Target { get; set; } = default!;

    /// <summary>Cancel, Escape or after a save.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Saved: the new song folder of the item (or of the track's item to follow), or null when unknown.</summary>
    [Parameter]
    public EventCallback<string?> OnSaved { get; set; }

    private LibraryInfoForm Form { get; } = new();

    private bool Loaded { get; set; }

    private bool Busy { get; set; }

    private string? Error { get; set; }

    private string ScopeName => Target.Scope == LibraryInfoScope.Track ? "track" : "item";

    private string Intro => Target.Scope == LibraryInfoScope.Track
        ? $"The names used to file every item of \"{Target.Subject}\" that has no own names."
        : $"The names used to file only \"{Target.Subject}\".";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await RunAsync(async () =>
        {
            Form.Load(await (Target.Scope == LibraryInfoScope.Track ? Api.GetTrackLibraryInfoAsync(Target.Id) : Api.GetAssetLibraryInfoAsync(Target.Id)));
            Loaded = true;
        });
    }

    private static void OnInput(LibraryInfoField field, ChangeEventArgs args)
    {
        field.Text = args.Value?.ToString() ?? string.Empty;
        field.Automatic = field.Text.Length == 0;
        field.Problem = null;
    }

    private static void OnAutomatic(LibraryInfoField field, ChangeEventArgs args)
    {
        field.Automatic = args.Value is true;
        field.Text = field.Automatic ? string.Empty : field.Text;
        field.Problem = null;
    }

    private Task OnKeyAsync(KeyboardEventArgs args) => CloseOnEscapeAsync(args, OnClose);

    private Task SaveAsync() => Form.Validate()
        ? RunAsync(async () => await FinishAsync(await (Target.Scope == LibraryInfoScope.Track
            ? Api.SetTrackLibraryInfoAsync(Target.Id, Form.Request())
            : Api.SetAssetLibraryInfoAsync(Target.Id, Form.Request()))))
        : Task.CompletedTask;

    private Task ResetAsync() => RunAsync(async () => await FinishAsync(await (Target.Scope == LibraryInfoScope.Track
        ? Api.ResetTrackLibraryInfoAsync(Target.Id)
        : Api.ResetAssetLibraryInfoAsync(Target.Id))));

    private async Task FinishAsync(LibraryInfoStateDto state) => await OnSaved.InvokeAsync(await NewSongFolderAsync(state));

    // The item's answer carries its new place; for a track the view follows one of its items without own names.
    private async Task<string?> NewSongFolderAsync(LibraryInfoStateDto state)
    {
        if (state.Placement is { } placement)
        {
            return placement.Song.FolderId;
        }

        if (Target.FollowAssetId is not { } follow)
        {
            return null;
        }

        var (item, _) = await ApiCall.TryAsync(() => Api.GetAssetLibraryInfoAsync(follow));
        return item?.Placement?.Song.FolderId;
    }

    private async Task RunAsync(Func<Task> action)
    {
        (Busy, Error) = (true, null);
        try
        {
            await action();
        }
        catch (ApiException ex)
        {
            Form.ShowErrors(ex.Error.FieldErrors);
            Error = ex.Error.UserMessage;
        }
        finally
        {
            Busy = false;
        }
    }
}
