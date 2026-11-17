using StemStudio.Agent.Backends;
using StemStudio.Agent.LyricAssist.Fakes;
using StemStudio.Agent.Ports;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>What the model layer of one assist is chosen for.</summary>
/// <param name="SheetTitle">Sheet title (selects the fake script <c>&lt;title-slug&gt;-&lt;kind&gt;.json</c>).</param>
/// <param name="Kind">Assist kind.</param>
public sealed record LyricAssistModelContext(string SheetTitle, AssistKindDto Kind);

/// <summary>The assistant of one assist, or why the model layer cannot run (the dictionary layer still runs, FR-027).</summary>
/// <param name="Assistant">The assistant; null when no model layer is available.</param>
/// <param name="UnavailableReason">The contract reason <c>model-unavailable</c> when <paramref name="Assistant"/> is null.</param>
/// <param name="Fix">Readable fix when <paramref name="Assistant"/> is null.</param>
public sealed record LyricAssistantResolution(ILyricAssistant? Assistant, string? UnavailableReason, string? Fix)
{
    /// <summary>An available assistant.</summary>
    /// <param name="assistant">The assistant.</param>
    /// <returns>The resolution.</returns>
    public static LyricAssistantResolution Of(ILyricAssistant assistant) => new(assistant, null, null);

    /// <summary>No model layer.</summary>
    /// <param name="fix">Readable fix.</param>
    /// <returns>The resolution.</returns>
    public static LyricAssistantResolution Unavailable(string fix) => new(null, LyricAssistProblemTypes.ModelUnavailable, fix);
}

/// <summary>Picks the model layer of one assist (CTO decision on the ai-agent / backend-023 seam).</summary>
public interface ILyricAssistantFactory
{
    /// <summary>The assistant for an assist, or the reason there is none.</summary>
    /// <param name="context">Sheet title and kind.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The resolution.</returns>
    ValueTask<LyricAssistantResolution> ResolveAsync(LyricAssistModelContext context, CancellationToken cancellationToken);
}

/// <summary>Dependencies of <see cref="LyricAssistantFactory"/> (one parameter object keeps the constructor small).</summary>
public sealed record LyricAssistantServices
{
    /// <summary>Agent settings (the selected backend).</summary>
    public required IAgentSettingsStore Settings { get; init; }

    /// <summary>Claude key state (environment over stored key; never read here beyond "usable").</summary>
    public required IClaudeKeyProvider ClaudeKeys { get; init; }

    /// <summary>The real model client.</summary>
    public required LyricModelClient Model { get; init; }

    /// <summary>Deterministic checks (backend adapter).</summary>
    public required ILyricVerification Verification { get; init; }

    /// <summary>Environment (the fake script variable).</summary>
    public required IEnvironmentReader Environment { get; init; }

    /// <summary>Limits.</summary>
    public LyricAssistOptions Options { get; init; } = new();

    /// <summary>Clock.</summary>
    public TimeProvider Time { get; init; } = TimeProvider.System;
}

/// <summary>
/// The model layer per assist: agent backend <c>fake</c> with <c>STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT</c> set -> a
/// <see cref="ScriptedLyricModel"/> of the script <see cref="LyricScript.Resolve"/> picks (no script = empty output for every block);
/// <c>ollama</c>, or <c>claude</c> with a usable key and the acknowledged agent notice -> <see cref="LyricModelClient"/>; anything
/// else -> no assistant with reason <c>model-unavailable</c>. Never switches to Claude on its own (FR-025).
/// </summary>
/// <param name="services">Dependencies.</param>
public sealed class LyricAssistantFactory(LyricAssistantServices services) : ILyricAssistantFactory
{
    /// <summary>The fake script variable (file or folder, contracts/DELTA.md section 5 item 3).</summary>
    public const string FakeScriptVariable = "STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT";

    /// <summary>Fix when the agent is off or no model backend is chosen.</summary>
    public const string ChooseBackendFix = "Choose Local AI (Ollama) in Settings to use the model layer.";

    /// <summary>Fix when the fake backend has no readable script.</summary>
    public const string FakeScriptFix = "Set STEMSTUDIO_LYRIC_ASSIST_FAKE_SCRIPT to a readable lyric script file or folder.";

    private const string EmptyScript = """{"version":1,"blocks":[]}""";

    /// <inheritdoc />
    public async ValueTask<LyricAssistantResolution> ResolveAsync(LyricAssistModelContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = await services.Settings.GetAsync(cancellationToken);
        return settings.Backend switch
        {
            AgentBackendDto.Ollama => Real(),
            AgentBackendDto.Claude => ClaudeResolution(settings),
            AgentBackendDto.Fake => Fake(context),
            _ => LyricAssistantResolution.Unavailable(ChooseBackendFix),
        };
    }

    private LyricAssistantResolution ClaudeResolution(Model.AgentSettings settings)
    {
        var key = services.ClaudeKeys.Resolve();
        if (key.Key is null)
        {
            return LyricAssistantResolution.Unavailable(key.Fix ?? ClaudeErrors.KeyMissingFix);
        }

        return settings.ClaudeNoticeAcknowledgedAt is null
            ? LyricAssistantResolution.Unavailable("Read and acknowledge the notice about the paid Claude service in Settings.")
            : Real();
    }

    private LyricAssistantResolution Real() => LyricAssistantResolution.Of(Assistant(services.Model));

    private LyricAssistantResolution Fake(LyricAssistModelContext context)
    {
        var path = services.Environment.GetVariable(FakeScriptVariable)?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            return LyricAssistantResolution.Unavailable(FakeScriptFix);
        }

        try
        {
            var file = LyricScript.Resolve(path, context.SheetTitle, context.Kind);
            var script = file is null ? LyricScript.Parse(EmptyScript) : LyricScript.Load(file);
            return LyricAssistantResolution.Of(Assistant(new ScriptedLyricModel(script)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return LyricAssistantResolution.Unavailable(FakeScriptFix);
        }
    }

    private LyricAssistant Assistant(ILyricModel model) => new(model, services.Verification, services.Options, services.Time);
}
