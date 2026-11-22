using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Scores;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>The drum notation of the trainer (FR-034): the 014 score view of a drum score item, or "Generate drum notation".</summary>
public sealed partial class DrumTrainer
{
    private static readonly ScoreProfileDto[] DrumProfiles = [ScoreProfileDto.Drums];

    /// <summary>The 014 score view of the drum item, shown when one exists (the mixer passes a <c>ScorePanel</c>).</summary>
    [Parameter]
    public RenderFragment? ScoreView { get; set; }
}
