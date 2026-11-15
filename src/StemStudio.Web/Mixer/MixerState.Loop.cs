using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>A-B loop handling of <see cref="MixerState"/> (FR-013, US2-4, US2-5).</summary>
public sealed partial class MixerState
{
    /// <summary>Sets marker A and revalidates.</summary>
    /// <param name="seconds">Position of A.</param>
    /// <returns>The region to send to the engine, or null when nothing must be sent.</returns>
    public LoopRegionDto? SetLoopStart(double seconds)
    {
        _loopStart = seconds;
        return Revalidate();
    }

    /// <summary>Sets marker B and revalidates.</summary>
    /// <param name="seconds">Position of B.</param>
    /// <returns>The region to send to the engine, or null when nothing must be sent.</returns>
    public LoopRegionDto? SetLoopEnd(double seconds)
    {
        _loopEnd = seconds;
        return Revalidate();
    }

    /// <summary>Turns looping on (when the markers are valid) or off.</summary>
    /// <returns>The region to send to the engine, or null when the loop was refused.</returns>
    public LoopRegionDto? ToggleLoop()
    {
        if (LoopEnabled)
        {
            return Commit(Loop! with { Enabled = false });
        }

        LoopError = MarkerError();
        return LoopError is null ? Commit(new LoopRegionDto(_loopStart!.Value, _loopEnd!.Value, true)) : null;
    }

    /// <summary>Records that the engine refused the loop; looping is shown as off.</summary>
    /// <param name="reason">The engine's message.</param>
    public void RejectLoop(string reason)
    {
        Loop = Loop is null ? null : Loop with { Enabled = false };
        LoopError = reason;
    }

    private LoopRegionDto? Revalidate()
    {
        IsDirty = true;
        if (_loopStart is null || _loopEnd is null)
        {
            return null;
        }

        LoopError = MarkerError();
        if (LoopError is null)
        {
            return Commit(new LoopRegionDto(_loopStart.Value, _loopEnd.Value, LoopEnabled));
        }

        return LoopEnabled ? Commit(Loop! with { Enabled = false }) : null;
    }

    private string? MarkerError() => _loopStart is { } start && _loopEnd is { } end
        ? LoopValidator.Validate(start, end, Duration)
        : LoopValidator.MissingMarkersMessage;

    private LoopRegionDto Commit(LoopRegionDto region)
    {
        Loop = region;
        IsDirty = true;
        return region;
    }
}
