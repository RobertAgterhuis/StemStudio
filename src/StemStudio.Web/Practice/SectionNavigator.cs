using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Sections;

namespace StemStudio.Web.Practice;

/// <summary>Where a section command moves the playback: the new position, the new loop (null: leave the loop) and the target section.</summary>
/// <param name="Position">Position to seek to (seconds).</param>
/// <param name="Loop">The loop to set, or null to leave the loop as it is.</param>
/// <param name="Section">The section moved to.</param>
public sealed record SectionJump(double Position, LoopRegionDto? Loop, SectionDto Section);

/// <summary>
/// Section navigation rules (014 data-model 5.3, FR-004): current = last section with start &lt;= position; next = first
/// section starting more than 50 ms ahead; previous = the current section's start when more than 2 s into it, else the
/// section before; restart = the current start (or 0). While an enabled loop holds the position, next/previous move the
/// loop to the neighbouring section (practise the song part by part).
/// </summary>
public static class SectionNavigator
{
    /// <summary>A section must start this far ahead to count as "next" (seconds).</summary>
    public const double NextMargin = 0.05;

    /// <summary>Further than this into a section, "previous" restarts it (seconds).</summary>
    public const double PreviousThreshold = 2;

    /// <summary>The section at a position, or null before the first section.</summary>
    /// <param name="sections">Ordered, non-overlapping sections.</param>
    /// <param name="position">Position in seconds.</param>
    /// <returns>The section, or null.</returns>
    public static SectionDto? Current(IReadOnlyList<SectionDto> sections, double position)
    {
        ArgumentNullException.ThrowIfNull(sections);
        return sections.LastOrDefault(s => s.StartSeconds <= position);
    }

    /// <summary>The next section, or null in the last one.</summary>
    /// <param name="sections">Ordered sections.</param>
    /// <param name="position">Position in seconds.</param>
    /// <returns>The section, or null.</returns>
    public static SectionDto? Next(IReadOnlyList<SectionDto> sections, double position) =>
        sections.FirstOrDefault(s => s.StartSeconds > position + NextMargin);

    /// <summary>The section "previous" goes to: the current one when more than 2 s into it, else the one before (null before the first).</summary>
    /// <param name="sections">Ordered sections.</param>
    /// <param name="position">Position in seconds.</param>
    /// <returns>The section, or null.</returns>
    public static SectionDto? Previous(IReadOnlyList<SectionDto> sections, double position)
    {
        var current = Current(sections, position);
        return current is not null && position - current.StartSeconds > PreviousThreshold ? current : Before(sections, current);
    }

    /// <summary>The start of the current section, or 0 before the first.</summary>
    /// <param name="sections">Ordered sections.</param>
    /// <param name="position">Position in seconds.</param>
    /// <returns>The position to seek to.</returns>
    public static double RestartPosition(IReadOnlyList<SectionDto> sections, double position) => Current(sections, position)?.StartSeconds ?? 0;

    /// <summary>The loop over a whole section, enabled, remembering the section (FR-005).</summary>
    /// <param name="section">The section.</param>
    /// <returns>The loop region.</returns>
    public static LoopRegionDto LoopOf(SectionDto section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return new LoopRegionDto(section.StartSeconds, section.EndSeconds, true, section.Id);
    }

    /// <summary>"Next section": inside an enabled loop the loop moves on, else a plain seek.</summary>
    /// <param name="sections">Ordered sections.</param>
    /// <param name="position">Position in seconds.</param>
    /// <param name="loop">The current loop, or null.</param>
    /// <returns>The jump, or null when there is no next section.</returns>
    public static SectionJump? NextJump(IReadOnlyList<SectionDto> sections, double position, LoopRegionDto? loop)
    {
        if (!Holds(loop, position))
        {
            return Next(sections, position) is { } next ? new SectionJump(next.StartSeconds, null, next) : null;
        }

        var after = sections.SkipWhile(s => s != LoopSection(sections, loop!, position)).Skip(1).FirstOrDefault();
        return after is null ? null : new SectionJump(after.StartSeconds, LoopOf(after), after);
    }

    /// <summary>"Previous section": inside an enabled loop the loop moves back one section, else <see cref="Previous"/>.</summary>
    /// <param name="sections">Ordered sections.</param>
    /// <param name="position">Position in seconds.</param>
    /// <param name="loop">The current loop, or null.</param>
    /// <returns>The jump, or null when there is no previous section.</returns>
    public static SectionJump? PreviousJump(IReadOnlyList<SectionDto> sections, double position, LoopRegionDto? loop)
    {
        if (!Holds(loop, position))
        {
            return Previous(sections, position) is { } previous ? new SectionJump(previous.StartSeconds, null, previous) : null;
        }

        var before = Before(sections, LoopSection(sections, loop!, position));
        return before is null ? null : new SectionJump(before.StartSeconds, LoopOf(before), before);
    }

    private static bool Holds(LoopRegionDto? loop, double position) =>
        loop is { Enabled: true } active && position >= active.Start - NextMargin && position <= active.End + NextMargin;

    private static SectionDto? LoopSection(IReadOnlyList<SectionDto> sections, LoopRegionDto loop, double position) =>
        sections.FirstOrDefault(s => s.Id == loop.SectionId) ?? Current(sections, position);

    private static SectionDto? Before(IReadOnlyList<SectionDto> sections, SectionDto? section) =>
        section is null ? null : sections.TakeWhile(s => s != section).LastOrDefault();
}
