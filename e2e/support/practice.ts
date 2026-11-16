/**
 * Shared knowledge of the feature 012 specs (practice tempo): the contract constants and formulas
 * of audio-engine.md 2.3.0 "Practice tempo", practice DTO builders, a recorder of the transport
 * position with its rate, an attribute watcher and the rate-aware karaoke reference.
 *
 * @packageDocumentation
 */
import type { Locator, Page } from '@playwright/test';
import type { MixSettingsDto, PracticeSettingsDto, StemName } from './api';
import { fakeStems } from './detail';
import { positionSeconds } from './levels';

/** `STRETCH_DELAY_FRAMES` (audio-engine.md, Phase 0 P0-1). */
export const stretchDelayFrames = 5120;

/** `STRETCH_CONTENT_FRAMES_PER_OCTAVE`. */
export const contentFramesPerOctave = -1100;

/** Sample rate the engine requests. */
export const engineSampleRate = 44_100;

/** Names of the two separately served module files (audio-engine.md "Packaging"). */
export const stretchModuleFiles = /(time-stretch-processor\.js|soundtouch\.wasm)(\?|$)/u;

const defaultTrainer: PracticeSettingsDto['trainer'] = { enabled: false, startPercent: 70, stepPercent: 5, targetPercent: 100, passesPerStep: 1 };

/** The defaults of backend-api 1.11.0 `PracticeSettings` (100 %, 0 st, trainer off, no count-in). */
export const defaultPractice: PracticeSettingsDto = { speedPercent: 100, transposeSemitones: 0, trainer: defaultTrainer, countIn: { bars: 0, everyPass: false } };

/** Changes to the defaults (nested groups are merged). */
export interface PracticeChanges {
  readonly speedPercent?: number;
  readonly transposeSemitones?: number;
  readonly trainer?: Partial<PracticeSettingsDto['trainer']>;
  readonly countIn?: Partial<PracticeSettingsDto['countIn']>;
}

/** {@link defaultPractice} with `changes` applied. */
export function practiceWith(changes: PracticeChanges): PracticeSettingsDto {
  return {
    speedPercent: changes.speedPercent ?? defaultPractice.speedPercent,
    transposeSemitones: changes.transposeSemitones ?? defaultPractice.transposeSemitones,
    trainer: { ...defaultPractice.trainer, ...changes.trainer },
    countIn: { ...defaultPractice.countIn, ...changes.countIn },
  };
}

/** What a seeded mix carries besides the default channels. */
export interface SeededMix {
  readonly name: string;
  readonly practice?: PracticeSettingsDto;
  readonly loop?: MixSettingsDto['loop'];
  readonly stems?: readonly StemName[];
}

/** Mix settings with every channel at 80 %, centre, the metronome off and (optionally) practice. */
export function mixSettings(mix: SeededMix): MixSettingsDto {
  const base: MixSettingsDto = {
    name: mix.name,
    masterVolume: 1,
    channels: (mix.stems ?? fakeStems).map((stem) => ({ stem, volume: 0.8, pan: 0, mute: false, solo: false })),
    metronome: { enabled: false, volume: 0.5, bpmOverride: null, offsetMs: 0, beatsPerBar: 4 },
    loop: mix.loop ?? null,
  };
  return mix.practice === undefined ? base : { ...base, practice: mix.practice };
}

/** Pitch factor of the stage: `2^(st/12) / (speed/100)`. */
export function pitchFactor(speedPercent: number, transposeSemitones: number): number {
  return 2 ** (transposeSemitones / 12) / (speedPercent / 100);
}

/** Heard latency `L(p) = (D + c(p)) / sampleRate` of an engaged stage, in seconds. */
export function heardLatencySeconds(speedPercent: number, transposeSemitones: number): number {
  const content = Math.round(contentFramesPerOctave * Math.log2(pitchFactor(speedPercent, transposeSemitones)));
  return (stretchDelayFrames + content) / engineSampleRate;
}

/** One reading of `transport-position`. */
export interface PositionSample {
  /** `performance.now()` of the change (ms). */
  readonly at: number;
  readonly seconds: number;
}

/** Records every change of the position text for `durationMs` (sampled in the page). */
export async function recordPositions(position: Locator, durationMs: number): Promise<PositionSample[]> {
  const raw = await position.evaluate(recordTextChanges, durationMs);
  return raw.map((r) => ({ at: r.at, seconds: positionSeconds(r.text) })).filter((s) => Number.isFinite(s.seconds));
}

/**
 * Track seconds per wall-clock second over the samples (least-squares slope); NaN with fewer than
 * three samples. The text changes at most 10 times a second (OnPositionChanged), so the slope is
 * far more precise than any single reading.
 */
export function positionRate(samples: readonly PositionSample[]): number {
  if (samples.length < 3) return Number.NaN;
  const xs = samples.map((s) => s.at / 1000);
  const ys = samples.map((s) => s.seconds);
  const mean = (v: readonly number[]): number => v.reduce((a, b) => a + b, 0) / v.length;
  const [mx, my] = [mean(xs), mean(ys)];
  const covariance = xs.reduce((sum, x, i) => sum + (x - mx) * ((ys[i] ?? my) - my), 0);
  const variance = xs.reduce((sum, x) => sum + (x - mx) ** 2, 0);
  return covariance / variance;
}

/** One recorded attribute snapshot of a watched element. */
export interface AttributeSample {
  readonly at: number;
  readonly values: Readonly<Record<string, string | null>>;
}

interface WatchWindow {
  __stemStudioWatched?: Record<string, AttributeSample[]>;
}

/**
 * Starts recording every change of `names` on the element with test id `testId` (also when it
 * is rendered later or re-rendered); read the samples with {@link readWatched}.
 */
export async function watchAttributes(page: Page, testId: string, names: readonly string[]): Promise<void> {
  await page.evaluate(startWatching, { testId, names: [...names] });
}

/** Samples recorded since {@link watchAttributes} for `testId`. */
export async function readWatched(page: Page, testId: string): Promise<AttributeSample[]> {
  return page.evaluate((id) => (window as WatchWindow).__stemStudioWatched?.[id] ?? [], testId);
}

/** The distinct consecutive values of one attribute in the samples (repeats collapsed). */
export function distinctValues(samples: readonly AttributeSample[], name: string): string[] {
  const values = samples.map((s) => s.values[name] ?? null).filter((v): v is string => v !== null);
  return values.filter((v, i) => i === 0 || v !== values[i - 1]);
}

/** First beat of the grid at or after `seconds` (track time); NaN when there is none. */
export function firstBeatAtOrAfter(beats: readonly number[], seconds: number): number {
  return beats.find((b) => b >= seconds - 1e-6) ?? Number.NaN;
}

/** Grid interval at the beat `beat` (to the next beat); NaN at the end of the grid. */
export function beatIntervalAt(beats: readonly number[], beat: number): number {
  const index = beats.indexOf(beat);
  const next = index < 0 ? undefined : beats[index + 1];
  return next === undefined ? Number.NaN : next - beat;
}

/**
 * Largest difference in ms between the transport anchors `offset - when * rate` of the running
 * music sources recorded by the alignment probe (`installAlignmentProbe`), i.e. how far apart the
 * stems are in track time; valid while the rate has not changed since the sources started and no
 * loop wrapped. `Infinity` when no source runs.
 */
export async function maxAnchorSpreadMs(page: Page): Promise<number> {
  return page.evaluate(() => {
    const all = (window as { __stemStudioSources?: { node: AudioBufferSourceNode; start: number; offset: number; stopped: boolean }[] })
      .__stemStudioSources ?? [];
    const running = all.filter((r) => !r.stopped && (r.node.buffer?.duration ?? 0) >= 1);
    const anchors = running.map((r) => r.offset - r.start * r.node.playbackRate.value);
    return anchors.length === 0 ? Number.POSITIVE_INFINITY : 1000 * (Math.max(...anchors) - Math.min(...anchors));
  });
}

/**
 * Replaces the karaoke probe's reference (support/karaoke-probe.ts) with the rate-aware formula
 * of 2.3.0: the heard position at `t` is `offset + (t - L - when) * rate`, wrapped into the loop,
 * where `L` is the stage latency of the chosen settings. Call after `installKaraokeProbe`.
 */
export async function installRateAwareReference(page: Page, latencySeconds: number): Promise<void> {
  await page.addInitScript(rateAwareReferenceInitScript, latencySeconds);
}

// The functions below are serialised into the page by Playwright: they must not reference
// anything outside their own bodies (types are erased and therefore allowed).
function recordTextChanges(el: Element, durationMs: number): Promise<{ at: number; text: string }[]> {
  return new Promise((resolve) => {
    const samples = [{ at: performance.now(), text: el.textContent }];
    const observer = new MutationObserver(() => samples.push({ at: performance.now(), text: el.textContent }));
    observer.observe(el, { childList: true, characterData: true, subtree: true, attributes: true });
    window.setTimeout(() => {
      observer.disconnect();
      resolve(samples);
    }, durationMs);
  });
}

function startWatching({ testId, names }: { testId: string; names: string[] }): void {
  const all = ((window as WatchWindow).__stemStudioWatched ??= {});
  const samples: AttributeSample[] = (all[testId] = []);
  const snapshot = (): void => {
    const el = document.querySelector(`[data-testid="${testId}"]`);
    const values = Object.fromEntries(names.map((n) => [n, el?.getAttribute(n) ?? null]));
    const last = samples.at(-1);
    if (last === undefined || names.some((n) => last.values[n] !== values[n])) samples.push({ at: performance.now(), values });
  };
  new MutationObserver(snapshot).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: names });
  snapshot();
}

function rateAwareReferenceInitScript(latency: number): void {
  interface Source {
    readonly node: AudioBufferSourceNode;
    readonly start: number;
    readonly offset: number;
    stopped: boolean;
  }
  const w = window as { __stemStudioSources?: Source[]; __stemStudioKaraokeReference?: () => number };
  const audioNow = (ctx: AudioContext): number => {
    const ts = ctx.getOutputTimestamp();
    const [context, perf] = [ts.contextTime ?? 0, ts.performanceTime ?? 0];
    return context > 0 && perf > 0 ? context + (performance.now() - perf) / 1000 : ctx.currentTime;
  };
  const wrap = (node: AudioBufferSourceNode, position: number): number => {
    const length = node.loopEnd - node.loopStart;
    return node.loop && length > 0 && position >= node.loopEnd ? node.loopStart + ((position - node.loopStart) % length) : position;
  };
  w.__stemStudioKaraokeReference = (): number => {
    const source = (w.__stemStudioSources ?? []).filter((r) => !r.stopped && (r.node.buffer?.duration ?? 0) >= 1).at(-1);
    if (source === undefined) return Number.NaN;
    const ctx = source.node.context as AudioContext;
    const heard = audioNow(ctx) - ((ctx as { outputLatency?: number }).outputLatency ?? 0) - latency;
    return Math.round(1000 * wrap(source.node, source.offset + (heard - source.start) * source.node.playbackRate.value));
  };
}
