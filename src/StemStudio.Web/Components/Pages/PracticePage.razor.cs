using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Practice;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Pages;

/// <summary>Practice page: the overview per track, the log switch, delete all and (Expert) the sessions.</summary>
public sealed partial class PracticePage
{
    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    private PracticeLogSettingsDto? Settings { get; set; }

    private IReadOnlyList<PracticeTrackTotalDto>? Overview { get; set; }

    private IReadOnlyList<PracticeSessionDto>? Sessions { get; set; }

    private string? Error { get; set; }

    private bool ConfirmDeleteAll { get; set; }

    private int Offset => (int)Time.LocalTimeZone.GetUtcOffset(Time.GetUtcNow()).TotalMinutes;

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => RunAsync(LoadAsync);

    private static string Iso(DateTimeOffset time) => time.ToString("O", CultureInfo.InvariantCulture);

    private string Date(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Time.LocalTimeZone).ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private string When(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, Time.LocalTimeZone).ToString("d MMM HH:mm", CultureInfo.InvariantCulture);

    private string TitleOf(Guid trackId) => Overview?.FirstOrDefault(o => o.TrackId == trackId)?.Title ?? "Song";

    private async Task LoadAsync()
    {
        Settings = await Api.GetPracticeLogSettingsAsync();
        Overview = await Api.GetPracticeOverviewAsync(Offset);
        Sessions = await Api.ListPracticeSessionsAsync(null);
    }

    private Task ToggleAsync() => RunAsync(async () =>
        Settings = await Api.UpdatePracticeLogSettingsAsync(new PracticeLogSettingsUpdateRequest { Enabled = Settings?.Enabled != true, NoticeAcknowledged = true }));

    private Task DeleteAllAsync() => RunAsync(async () =>
    {
        ConfirmDeleteAll = false;
        await Api.DeletePracticeSessionsAsync(null);
        await LoadAsync();
    });

    private Task DeleteSessionAsync(Guid sessionId) => RunAsync(async () =>
    {
        await Api.DeletePracticeSessionAsync(sessionId);
        await LoadAsync();
    });

    private async Task RunAsync(Func<Task> call)
    {
        try
        {
            await call();
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }
}
