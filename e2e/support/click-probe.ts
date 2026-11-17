/**
 * Click probe of feature 012 (T012; SC-005, FR-012, quickstart P7, P9).
 *
 * An init script wraps `AudioBufferSourceNode.prototype.start/stop` and records every source on
 * the context clock: clicks (buffers shorter than {@link maxClickSeconds}: the metronome and the
 * count-in) with their planned `start(when)` time and the peak of their buffer (the accent buffer
 * is the louder one, audio-engine.md "Metronome"), and music sources (buffers of at least one
 * second: stems) with `when`, buffer offset and playback rate. A click stopped at or before its
 * planned time counts as cancelled (seek, pause, rate change). Clicks bypass the stretch stage,
 * so their `when` is the time they are heard; music sources start `L(p)` before they are heard.
 *
 * @packageDocumentation
 */
import type { Page } from '@playwright/test';

/** Buffers shorter than this are clicks. */
export const maxClickSeconds = 0.25;

/** One scheduled click. */
export interface ScheduledClick {
  /** Planned context time (`max(when, currentTime)` at the call). */
  readonly when: number;
  /** Context time of the `start` call. */
  readonly calledAt: number;
  /** Peak of the click buffer (accent clicks use the louder buffer). */
  readonly peak: number;
  /** Stopped at or before `when`: never heard. */
  readonly cancelled: boolean;
}

/** One started music (stem) source. */
export interface MusicStart {
  readonly when: number;
  readonly offset: number;
  readonly rate: number;
  readonly calledAt: number;
}

/** Everything the probe recorded so far. */
export interface ClickRecording {
  readonly clicks: readonly ScheduledClick[];
  readonly music: readonly MusicStart[];
  /** `currentTime` of the context that started the last source (0 when none). */
  readonly now: number;
}

interface ClickRecord {
  readonly node: AudioBufferSourceNode;
  readonly when: number;
  readonly calledAt: number;
  readonly peak: number;
  stoppedAt: number;
}

interface ClickProbeState {
  readonly clicks: ClickRecord[];
  readonly music: MusicStart[];
  context?: BaseAudioContext;
}

interface ClickProbeWindow {
  __stemStudioClickProbe?: ClickProbeState;
}

/** Installs the probe before any page script runs (call before `page.goto`). */
export async function installClickProbe(page: Page): Promise<void> {
  await page.addInitScript(clickStartInitScript, maxClickSeconds);
  await page.addInitScript(clickStopInitScript);
}

/** Installs the probe into the page's current document (probe self-tests on a blank page). */
export async function installClickProbeNow(page: Page): Promise<void> {
  await page.evaluate(clickStartInitScript, maxClickSeconds);
  await page.evaluate(clickStopInitScript);
}

/** Everything recorded so far. */
export async function readClicks(page: Page): Promise<ClickRecording> {
  return page.evaluate(() => {
    const state = (window as ClickProbeWindow).__stemStudioClickProbe ?? { clicks: [], music: [] };
    const clicks = state.clicks.map((c) => ({ when: c.when, calledAt: c.calledAt, peak: c.peak, cancelled: c.stoppedAt <= c.when }));
    return { clicks, music: [...state.music], now: state.context?.currentTime ?? 0 };
  });
}

/**
 * Clicks that are heard (not cancelled) in `[from, until)`, in time order. Pass `until =
 * recording.now` to keep only clicks already played (a future click can still be cancelled).
 */
export function heardClicks(recording: ClickRecording, from = 0, until = Number.POSITIVE_INFINITY): ScheduledClick[] {
  return recording.clicks.filter((c) => !c.cancelled && c.when >= from && c.when < until).sort((a, b) => a.when - b.when);
}

/** True per click when it used the louder (accent) buffer of the recording. */
export function accents(clicks: readonly ScheduledClick[]): boolean[] {
  const loudest = Math.max(...clicks.map((c) => c.peak));
  const quietest = Math.min(...clicks.map((c) => c.peak));
  return clicks.map((c) => loudest > quietest && c.peak === loudest);
}

/** Differences between consecutive click times in seconds. */
export function spacings(clicks: readonly ScheduledClick[]): number[] {
  return clicks.slice(1).map((c, i) => c.when - (clicks[i]?.when ?? c.when));
}

/**
 * Start times of the passes (or restarts) of the music: music sources whose `when` lies within
 * 1 ms of each other belong to one start (all stems start together, FR-004).
 */
export function musicStartTimes(recording: ClickRecording, from = 0): number[] {
  const times = recording.music.map((m) => m.when).filter((t) => t >= from).sort((a, b) => a - b);
  return times.filter((t, i) => i === 0 || t - (times[i - 1] ?? t) > 0.001);
}

// The init scripts are serialised into the page by Playwright: they must not reference anything
// outside their own bodies (types are erased and therefore allowed).
function clickStartInitScript(maxSeconds: number): void {
  const state: ClickProbeState = { clicks: [], music: [] };
  (window as ClickProbeWindow).__stemStudioClickProbe = state;
  const peaks = new WeakMap<AudioBuffer, number>();
  const peakOf = (buffer: AudioBuffer): number => {
    const known = peaks.get(buffer);
    if (known !== undefined) return known;
    const peak = buffer.getChannelData(0).reduce((max, v) => Math.max(max, Math.abs(v)), 0);
    peaks.set(buffer, peak);
    return peak;
  };
  const record = (node: AudioBufferSourceNode, when: number, offset: number): void => {
    const [now, buffer] = [node.context.currentTime, node.buffer];
    state.context = node.context;
    if (buffer === null) return;
    if (buffer.duration < maxSeconds) {
      state.clicks.push({ node, when: Math.max(when, now), calledAt: now, peak: peakOf(buffer), stoppedAt: Infinity });
    } else if (buffer.duration >= 1) {
      state.music.push({ when: Math.max(when, now), offset, rate: node.playbackRate.value, calledAt: now });
    }
  };
  type Start = (this: AudioBufferSourceNode, ...args: unknown[]) => unknown;
  const methods = AudioBufferSourceNode.prototype as unknown as { start: Start };
  const start = methods.start;
  methods.start = function (this: AudioBufferSourceNode, ...args: unknown[]): unknown {
    const [when, offset] = args as [number?, number?];
    record(this, when ?? 0, offset ?? 0);
    return start.apply(this, args);
  };
}

function clickStopInitScript(): void {
  type Stop = (this: AudioBufferSourceNode, ...args: unknown[]) => unknown;
  const methods = AudioBufferSourceNode.prototype as unknown as { stop: Stop };
  const stop = methods.stop;
  methods.stop = function (this: AudioBufferSourceNode, ...args: unknown[]): unknown {
    const [when = 0] = args as [number?];
    const at = Math.max(when, this.context.currentTime);
    const clicks = (window as ClickProbeWindow).__stemStudioClickProbe?.clicks ?? [];
    clicks.filter((c) => c.node === this).forEach((c) => (c.stoppedAt = Math.min(c.stoppedAt, at)));
    return stop.apply(this, args);
  };
}
