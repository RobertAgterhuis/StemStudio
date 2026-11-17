// TEMPORARY (backend-023 lane, CTO decision 2026-11-17): exact copy of the ai-agent seam that lands in
// src/StemStudio.Agent/LyricAssist/. Delete this whole file when the ai-agent lane is merged (the duplicate types make the
// build fail until it is removed). Nothing else of the API declares types in StemStudio.Agent.
using System.Diagnostics.CodeAnalysis;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>What the factory needs to pick the model layer of one assist (pending ai-agent seam).</summary>
/// <param name="SheetTitle">Sheet title (selects a fake script).</param>
/// <param name="Kind">Assist kind.</param>
[ExcludeFromCodeCoverage]
public sealed record LyricAssistModelContext(string SheetTitle, AssistKindDto Kind);

/// <summary>The model layer of one assist; <see cref="Assistant"/> null = unavailable (pending ai-agent seam).</summary>
/// <param name="Assistant">The assistant, or null.</param>
/// <param name="UnavailableReason">Why the model layer is unavailable.</param>
/// <param name="Fix">How to fix it.</param>
[ExcludeFromCodeCoverage]
public sealed record LyricAssistantResolution(ILyricAssistant? Assistant, string? UnavailableReason, string? Fix);

/// <summary>Picks the model layer for one assist (pending ai-agent seam).</summary>
public interface ILyricAssistantFactory
{
    /// <summary>Resolves the assistant of one assist.</summary>
    /// <param name="context">The assist context.</param>
    /// <returns>The resolution.</returns>
    LyricAssistantResolution Resolve(LyricAssistModelContext context);
}

/// <summary>Registers the ai-agent part of the lyric assistant (pending ai-agent seam: registers nothing yet).</summary>
[ExcludeFromCodeCoverage]
public static class LyricAssistServiceCollectionExtensions
{
    /// <summary>Registers the assistant factory.</summary>
    /// <param name="services">Services.</param>
    /// <returns>The same services.</returns>
    public static IServiceCollection AddLyricAssistAgent(this IServiceCollection services) => services;
}
