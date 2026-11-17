namespace StemStudio.Web.Audio;

/// <summary>Argument of the engine callback <c>OnLoopPass</c> (audio-engine 2.4.0 <c>LoopPassInfo</c>): one completed pass of an enabled loop.</summary>
/// <param name="Start">Loop start of the pass (seconds).</param>
/// <param name="End">Loop end of the pass (seconds).</param>
/// <param name="Pass">1-based pass number since looping was (re-)enabled or the region changed.</param>
/// <param name="SpeedPercent">Playback speed of that pass in percent (100 without 012 speed).</param>
public sealed record LoopPassInfo(double Start, double End, int Pass, double SpeedPercent);
