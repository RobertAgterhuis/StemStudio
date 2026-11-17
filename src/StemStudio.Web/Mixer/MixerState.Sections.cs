using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Sections;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Mixer;

/// <summary>Song sections in the mixer (feature 014 US1, FR-004/FR-005): the section set and the section loop.</summary>
public sealed partial class MixerState
{
    /// <summary>The track's section set (null until loaded).</summary>
    public SectionSetDto? SectionSet { get; set; }

    /// <summary>The ordered sections (empty until detected).</summary>
    public IReadOnlyList<SectionDto> Sections => SectionSet?.Sections ?? [];

    /// <summary>The section the loop came from (FR-005), while the loop still covers it.</summary>
    public SectionDto? LoopedSection => Loop is { SectionId: { } id } ? Sections.FirstOrDefault(s => s.Id == id) : null;

    /// <summary>Loops a whole section and remembers it (until A or B is moved by hand).</summary>
    /// <param name="section">The section.</param>
    /// <returns>The new loop.</returns>
    public LoopRegionDto LoopSection(SectionDto section)
    {
        var loop = SectionNavigator.LoopOf(section);
        (_loopStart, _loopEnd, LoopError) = (loop.Start, loop.End, null);
        return Commit(loop);
    }
}
