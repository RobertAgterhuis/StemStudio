using Microsoft.Extensions.AI;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Runtime;

/// <summary>
/// Executes an undo run: the newest group's entries in reverse step order through the step executor (policy, approval,
/// events), without a model turn. Applied entries mark their original step <c>undone</c>; entries whose target changed or
/// vanished, and calls that failed or were rejected, become <c>obsolete</c> and are named in the answer.
/// </summary>
/// <param name="services">Ports and services.</param>
/// <param name="events">Event publisher.</param>
/// <param name="steps">Step executor.</param>
internal sealed class UndoRun(AgentServices services, RunEvents events, StepExecutor steps)
{
    private const string NotAvailable = "the undo action is not available";

    public async Task<RunOutcome> ExecuteAsync(RunState state, CancellationToken cancellationToken)
    {
        await events.PublishAsync(state.RunId, AgentEventTypes.RunStarted, new RunStartedEventDto { RunId = state.RunId, Backend = state.Run.Backend, Model = UndoPlanner.UndoModel, UsingFallback = false }, cancellationToken);
        var group = await UndoPlanner.NewestGroupAsync(services.Undo, state.Run.ConversationId, cancellationToken);
        var notUndone = await ApplyGroupAsync(state, group, cancellationToken);
        state.Answer.Append(Summary(group.Count - notUndone.Count, notUndone));
        var message = await RunLoader.AppendAnswerAsync(services, state, cancellationToken);
        return RunOutcome.Completed(message.Id);
    }

    /// <summary>Applies the entries in order; returns the ones not undone with their reasons.</summary>
    private async Task<List<string>> ApplyGroupAsync(RunState state, IReadOnlyList<UndoEntry> group, CancellationToken cancellationToken)
    {
        var notUndone = new List<string>();
        foreach (var entry in group)
        {
            state.CurrentUndo = entry;
            if (await ApplyAsync(state, entry, cancellationToken) is { } reason)
            {
                await services.Undo.SetStatusAsync(entry.Id, UndoEntryStatus.Obsolete, cancellationToken);
                notUndone.Add($"{entry.Action.Tool} ({reason})");
            }
        }

        state.CurrentUndo = null;
        return notUndone;
    }

    /// <summary>Applies one entry; returns why it was not undone, or null when it was.</summary>
    private async Task<string?> ApplyAsync(RunState state, UndoEntry entry, CancellationToken cancellationToken)
    {
        var (arguments, guard) = UndoGuard.For(entry);
        var tool = FindTool(entry.Action.Tool);
        if ((tool is null ? NotAvailable : await TargetChangedAsync(guard, cancellationToken)) is { } blocked)
        {
            return blocked;
        }

        var step = await steps.ExecuteUndoAsync(state, Prepare(state, tool!, arguments), cancellationToken);
        return step.Status == AgentStepStatusDto.Succeeded ? await AppliedAsync(entry, cancellationToken) : step.Error ?? "the step did not run";
    }

    private AgentToolDescriptor? FindTool(string name) => services.Registry.Find(name) ?? UndoTools.All.FirstOrDefault(item => item.Name == name);

    private async Task<string?> AppliedAsync(UndoEntry entry, CancellationToken cancellationToken)
    {
        await services.Undo.SetStatusAsync(entry.Id, UndoEntryStatus.Applied, cancellationToken);
        await MarkUndoneAsync(entry, cancellationToken);
        return null;
    }

    /// <summary>The recorded call with trusted arguments (from the undo store, not from a model).</summary>
    private static PreparedCall Prepare(RunState state, AgentToolDescriptor tool, string arguments)
    {
        var validation = new ArgumentValidation(true, UndoGuard.Element(arguments), arguments, CanonicalJson.Hash(arguments), null);
        return new PreparedCall(new FunctionCallContent($"undo-{state.StepSeq + 1}", tool.Name), tool, validation);
    }

    private Task<string?> TargetChangedAsync(UndoGuard? guard, CancellationToken cancellationToken) =>
        guard is null ? Task.FromResult<string?>(null) : MixChangedAsync(guard, cancellationToken);

    private async Task<string?> MixChangedAsync(UndoGuard guard, CancellationToken cancellationToken)
    {
        try
        {
            var mix = await services.Actions.Mixes.GetMixAsync(guard.MixId, cancellationToken);
            return guard.ChangedSince(mix.UpdatedAt) ? UndoPlanner.ChangedOutside : null;
        }
        catch (AgentActionException exception)
        {
            return exception.IsNotFound ? UndoPlanner.Vanished : exception.Message;
        }
    }

    private async Task MarkUndoneAsync(UndoEntry entry, CancellationToken cancellationToken)
    {
        var original = (await services.Runs.GetStepsAsync(entry.RunId, cancellationToken)).FirstOrDefault(step => step.Id == entry.StepId);
        if (original is not null)
        {
            await services.Runs.SaveStepAsync(original with { Status = AgentStepStatusDto.Undone }, cancellationToken);
        }
    }

    private static string Summary(int undone, List<string> notUndone)
    {
        var text = $"Undone: {undone} {(undone == 1 ? "step" : "steps")}.";
        return notUndone.Count == 0 ? text : $"{text} Not undone: {string.Join("; ", notUndone)}";
    }
}
