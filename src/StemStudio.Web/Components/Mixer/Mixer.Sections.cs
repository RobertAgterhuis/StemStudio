using StemStudio.Contracts.Sections;
using StemStudio.Web.Mixer;
using StemStudio.Web.Practice;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Song sections of the mixer page (014 US1, FR-004/FR-005): the lane's actions and the section commands.</summary>
public sealed partial class Mixer
{
    private bool EditingSections { get; set; }

    private bool ConfirmOverwrite { get; set; }

    private static string ConfidenceText(double confidence) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Math.Round(confidence * 100)} %");

    private void OnSectionsLoaded(SectionSetDto set) => State!.SectionSet = set;

    private void OpenSectionEditor() => EditingSections = !EditingSections;

    private void CloseSectionEditor() => EditingSections = false;

    private void OnSectionsSaved(SectionSetDto set)
    {
        State!.SectionSet = set;
        EditingSections = false;
        Toasts.Show("Sections saved");
    }

    // Edited sections are replaced only after a confirmation (FR-006).
    private Task DetectAgainAsync()
    {
        if (State!.SectionSet?.Origin == SectionSetOriginDto.Edited)
        {
            ConfirmOverwrite = true;
            return Task.CompletedTask;
        }

        return DetectAsync(false);
    }

    private async Task DetectAsync(bool overwrite)
    {
        ConfirmOverwrite = false;
        EditingSections = false;
        try
        {
            State!.SectionSet = await Api.DetectSectionsAsync(Id, overwrite);
        }
        catch (Api.ApiException ex)
        {
            Toasts.Show(ex.Error.UserMessage);
        }
    }

    // One click loops the section; the playhead moves into it when it is outside (FR-004).
    private async Task LoopSectionAsync(SectionDto section)
    {
        await SendLoopAsync(State!.LoopSection(section));
        if (Position < section.StartSeconds || Position >= section.EndSeconds)
        {
            await SeekAsync(section.StartSeconds);
        }
    }

    private void RegisterSectionCommands(MixerCommandRegistry commands)
    {
        commands.Add("mixer.loop.section", () => SectionNavigator.Current(State!.Sections, Position) is { } section
            ? LoopSectionAsync(section)
            : Task.CompletedTask);
        commands.Add("mixer.section.next", () => JumpAsync(SectionNavigator.NextJump(State!.Sections, Position, State.Loop)));
        commands.Add("mixer.section.previous", () => JumpAsync(SectionNavigator.PreviousJump(State!.Sections, Position, State.Loop)));
        commands.Add("mixer.section.restart", () => State!.Sections.Count == 0
            ? Task.CompletedTask
            : SeekAsync(SectionNavigator.RestartPosition(State.Sections, Position)));
    }

    private async Task JumpAsync(SectionJump? jump)
    {
        if (jump is null)
        {
            return;
        }

        if (jump.Loop is not null)
        {
            await SendLoopAsync(State!.LoopSection(jump.Section));
        }

        await SeekAsync(jump.Position);
    }

    // "Region: Guitar solo (0:40-0:56)" with a section loop, "Region: A-B loop (0:40-0:56)" otherwise (A-012-3).
    private string? TrainerRegion => State!.Loop is { } loop
        ? $"Region: {State.LoopedSection?.Label ?? "A-B loop"} ({TimeText.Duration(loop.Start)}-{TimeText.Duration(loop.End)})"
        : null;

    private Task SectionCommandAsync(string command) => ActionAsync(() => _commands.DispatchAsync(command));
}
