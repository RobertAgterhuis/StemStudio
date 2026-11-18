using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Practice;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Practice;

/// <summary>Code-behind of the practice log panel: loads the switch and the 30-day summary of the track.</summary>
public sealed partial class PracticeLogPanel
{
    /// <summary>Days of the summary window.</summary>
    public const int Days = 30;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    /// <summary>Track.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>Raised with the new settings after a switch change (the mixer starts its recorder when the log turns on).</summary>
    [Parameter]
    public EventCallback<PracticeLogSettingsDto> OnSettingsChanged { get; set; }

    private PracticeLogSettingsDto? Settings { get; set; }

    private PracticeSummaryDto? Summary { get; set; }

    private string? Error { get; set; }

    private bool ConfirmDelete { get; set; }

    /// <summary>The local UTC offset in minutes (the time zone of the app's host: StemStudio runs on the user's computer).</summary>
    private int Offset => (int)Time.LocalTimeZone.GetUtcOffset(Time.GetUtcNow()).TotalMinutes;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => RunAsync(async () =>
    {
        Settings = await Api.GetPracticeLogSettingsAsync();
        Summary = await Api.GetPracticeSummaryAsync(TrackId, Days, Offset);
    });

    private static string BestText(double? percent) => percent is { } best ? best.ToString("0", CultureInfo.InvariantCulture) + " %" : "-";

    private Task UpdateAsync(bool? enabled = null, bool? acknowledged = null) => RunAsync(async () =>
    {
        Settings = await Api.UpdatePracticeLogSettingsAsync(new PracticeLogSettingsUpdateRequest { Enabled = enabled, NoticeAcknowledged = acknowledged });
        await OnSettingsChanged.InvokeAsync(Settings);
    });

    private Task DeleteAsync() => RunAsync(async () =>
    {
        ConfirmDelete = false;
        await Api.DeletePracticeSessionsAsync(TrackId);
        Summary = await Api.GetPracticeSummaryAsync(TrackId, Days, Offset);
    });

    private async Task RunAsync(Func<Task> call) => Error = await ApiCall.RunAsync(call);
}
