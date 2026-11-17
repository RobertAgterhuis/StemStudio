/**
 * Output probe of feature 012 (T012; SC-001, SC-002, quickstart P4, P5, P14).
 *
 * Init scripts wrap `AudioNode.prototype.connect/disconnect` before any page script runs: every
 * connection to an `AudioDestinationNode` is also made to two `AnalyserNode`s of that context
 * (a fine one with `fftSize` 32768 for frequency, a fast one with 2048 for change timing and
 * level), which feed a silent sink. The probe therefore hears exactly what reaches the speakers
 * (the master bus after the stretch stage, and the metronome), without touching the engine.
 *
 * The dominant frequency is the highest bin above 50 Hz, refined by parabolic interpolation over
 * the dB spectrum (bin width 1.35 Hz for the fine analyser at 44.1 kHz).
 *
 * @packageDocumentation
 */
import type { Locator, Page } from '@playwright/test';

/** FFT size of the frequency analyser (0.74 s at 44.1 kHz). */
export const fineFftSize = 32_768;

/** FFT size of the change-timing analyser (46 ms at 44.1 kHz). */
export const fastFftSize = 2_048;

/** Dominant frequency and its level. */
export interface Dominant {
  /** Interpolated frequency in Hz (NaN when nothing reaches the output). */
  readonly hz: number;
  /** Level of the peak bin in dB (-Infinity when nothing reaches the output). */
  readonly db: number;
}

/** What a timed action does to its control inside the page. */
export type OutputAction = { readonly kind: 'click' } | { readonly kind: 'value'; readonly value: number };

/** A frequency step whose arrival at the output is timed. */
export interface FrequencyStep {
  readonly action: OutputAction;
  readonly fromHz: number;
  readonly toHz: number;
  /** Give up after this many milliseconds (default 1000). */
  readonly windowMs?: number;
}

/** Which analyser a reading uses. */
export type AnalyserKind = 'fine' | 'fast';

interface OutputTap {
  readonly context: BaseAudioContext;
  readonly fine: AnalyserNode;
  readonly fast: AnalyserNode;
}

interface OutputProbeWindow {
  __stemStudioOutputTaps?: OutputTap[];
  __stemStudioOutputDominant?: (kind: AnalyserKind) => Dominant;
  __stemStudioOutputRmsDb?: () => number;
}

/** Installs the probe before any page script runs (call before `page.goto`). */
export async function installOutputProbe(page: Page): Promise<void> {
  await page.addInitScript(tapInitScript, { fine: fineFftSize, fast: fastFftSize });
  await page.addInitScript(untapInitScript);
  await page.addInitScript(dominantInitScript);
  await page.addInitScript(rmsInitScript);
}

/**
 * Installs the probe into the page's current document (for probe self-tests on a blank page;
 * only connections made afterwards are tapped).
 */
export async function installOutputProbeNow(page: Page): Promise<void> {
  await page.evaluate(tapInitScript, { fine: fineFftSize, fast: fastFftSize });
  await page.evaluate(untapInitScript);
  await page.evaluate(dominantInitScript);
  await page.evaluate(rmsInitScript);
}

/** Dominant frequency at the output now (fine analyser unless stated). */
export async function outputDominant(page: Page, kind: AnalyserKind = 'fine'): Promise<Dominant> {
  return page.evaluate((which) => {
    const read = (window as OutputProbeWindow).__stemStudioOutputDominant;
    return read === undefined ? { hz: Number.NaN, db: Number.NEGATIVE_INFINITY } : read(which);
  }, kind);
}

/** RMS level of the last 46 ms at the output in dBFS (-Infinity when silent or not tapped). */
export async function outputRmsDb(page: Page): Promise<number> {
  return page.evaluate(() => (window as OutputProbeWindow).__stemStudioOutputRmsDb?.() ?? Number.NEGATIVE_INFINITY);
}

/**
 * Performs `step.action` on `control` inside the page and returns the milliseconds until the
 * fast analyser's dominant frequency is closer to `toHz` than to `fromHz` in three consecutive
 * readings (time of the first of them); `Infinity` when that does not happen within the window.
 * Both timestamps are taken in the page, so Playwright's round trips are not part of the result;
 * the analyser window (46 ms) delays the detection by up to half its length.
 */
export async function timeFrequencyStep(control: Locator, step: FrequencyStep): Promise<number> {
  return control.evaluate(actAndWatch, { ...step, windowMs: step.windowMs ?? 1000 });
}

/** True when `actual` is within `tolerance` (fraction, e.g. 0.01) of `expected`. */
export function withinRatio(actual: number, expected: number, tolerance: number): boolean {
  return Math.abs(actual / expected - 1) <= tolerance;
}

/** Frequency of `baseHz` transposed by `semitones` (equal temperament). */
export function transposedHz(baseHz: number, semitones: number): number {
  return baseHz * 2 ** (semitones / 12);
}

// The init scripts and actAndWatch are serialised into the page by Playwright: they must not
// reference anything outside their own bodies (types are erased and therefore allowed).
function tapInitScript(sizes: { readonly fine: number; readonly fast: number }): void {
  const taps: OutputTap[] = [];
  (window as OutputProbeWindow).__stemStudioOutputTaps = taps;
  type Connect = (this: AudioNode, ...args: unknown[]) => unknown;
  const proto = AudioNode.prototype as unknown as { connect: Connect };
  const connect = proto.connect;
  const tapOf = (context: BaseAudioContext): OutputTap => {
    const known = taps.find((t) => t.context === context);
    if (known !== undefined) return known;
    const make = (fftSize: number): AnalyserNode => new AnalyserNode(context, { fftSize, smoothingTimeConstant: 0 });
    const tap: OutputTap = { context, fine: make(sizes.fine), fast: make(sizes.fast) };
    const sink = new GainNode(context, { gain: 0 });
    connect.call(tap.fine, sink);
    connect.call(tap.fast, sink);
    connect.call(sink, context.destination);
    taps.push(tap);
    return tap;
  };
  proto.connect = function (this: AudioNode, ...args: unknown[]): unknown {
    const result = connect.apply(this, args);
    if (args[0] instanceof AudioDestinationNode) {
      const tap = tapOf(this.context);
      connect.call(this, tap.fine);
      connect.call(this, tap.fast);
    }
    return result;
  };
}

function untapInitScript(): void {
  type Disconnect = (this: AudioNode, ...args: unknown[]) => unknown;
  const proto = AudioNode.prototype as unknown as { disconnect: Disconnect };
  const disconnect = proto.disconnect;
  const quietly = (node: AudioNode, from: AudioNode): void => {
    try {
      disconnect.call(node, from);
    } catch {
      // Not connected to this analyser (already disconnected).
    }
  };
  proto.disconnect = function (this: AudioNode, ...args: unknown[]): unknown {
    const result = disconnect.apply(this, args);
    const tap = (window as OutputProbeWindow).__stemStudioOutputTaps?.find((t) => t.context === this.context);
    if (args[0] instanceof AudioDestinationNode && tap !== undefined) {
      quietly(this, tap.fine);
      quietly(this, tap.fast);
    }
    return result;
  };
}

function dominantInitScript(): void {
  const peakBin = (bins: Float32Array, from: number): number => {
    let best = from;
    for (let i = from + 1; i < bins.length - 1; i++) {
      if ((bins[i] ?? -Infinity) > (bins[best] ?? -Infinity)) best = i;
    }
    return best;
  };
  const refine = (bins: Float32Array, at: number): number => {
    const [a, b, c] = [bins[at - 1] ?? 0, bins[at] ?? 0, bins[at + 1] ?? 0];
    const curvature = a - 2 * b + c;
    return curvature === 0 ? at : at + (0.5 * (a - c)) / curvature;
  };
  (window as OutputProbeWindow).__stemStudioOutputDominant = (kind: AnalyserKind): Dominant => {
    const tap = (window as OutputProbeWindow).__stemStudioOutputTaps?.at(-1);
    if (tap === undefined) return { hz: Number.NaN, db: Number.NEGATIVE_INFINITY };
    const node = tap[kind];
    const bins = new Float32Array(node.frequencyBinCount);
    node.getFloatFrequencyData(bins);
    const binHz = tap.context.sampleRate / node.fftSize;
    const at = peakBin(bins, Math.ceil(50 / binHz));
    const db = bins[at] ?? Number.NEGATIVE_INFINITY;
    return Number.isFinite(db) ? { hz: refine(bins, at) * binHz, db } : { hz: Number.NaN, db };
  };
}

function rmsInitScript(): void {
  (window as OutputProbeWindow).__stemStudioOutputRmsDb = (): number => {
    const tap = (window as OutputProbeWindow).__stemStudioOutputTaps?.at(-1);
    if (tap === undefined) return Number.NEGATIVE_INFINITY;
    const samples = new Float32Array(tap.fast.fftSize);
    tap.fast.getFloatTimeDomainData(samples);
    const power = samples.reduce((sum, v) => sum + v * v, 0) / samples.length;
    return 10 * Math.log10(power);
  };
}

function actAndWatch(el: Element, step: Required<FrequencyStep>): Promise<number> {
  const read = (window as OutputProbeWindow).__stemStudioOutputDominant;
  if (read === undefined) throw new Error('output probe not installed (call installOutputProbe before page.goto)');
  const nearer = (): boolean => {
    const { hz } = read('fast');
    return Math.abs(hz - step.toHz) < Math.abs(hz - step.fromHz);
  };
  const start = performance.now();
  if (step.action.kind === 'click') {
    (el as HTMLElement).click();
  } else {
    (el as HTMLInputElement).value = String(step.action.value);
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  }
  return new Promise((resolve) => {
    const hits: number[] = [];
    const check = (): void => {
      const now = performance.now();
      if (nearer()) hits.push(now);
      else hits.length = 0;
      if (hits.length >= 3) resolve((hits[0] ?? now) - start);
      else if (now - start > step.windowMs) resolve(Number.POSITIVE_INFINITY);
      else window.setTimeout(check, 2);
    };
    check();
  });
}
