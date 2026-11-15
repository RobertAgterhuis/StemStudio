/**
 * Karaoke landing probe (feature 006, SC-002, quickstart L4).
 *
 * The 002 alignment probe (`support/alignment-probe.ts`) wraps `AudioBufferSourceNode.start/stop`
 * so the page knows every stem source's start time and buffer offset on the context clock. A
 * `MutationObserver` on the `karaoke-banner` host then records each change of `data-word-index`
 * / `data-line-index` / `data-state` together with an **independent** reference position computed
 * with the audio-engine.md formula: `offset + (audioNow - start)` wrapped into the source's loop,
 * `audioNow` from `getOutputTimestamp()` extrapolated with `performance.now()`, minus
 * `outputLatency`. The landing error of a word change is `reference - offsetMs - word.startMs`.
 *
 * Assumption (to confirm with audio-engine 006:T035): `data-word-index` counts words inside the
 * line given by `data-line-index`.
 *
 * @packageDocumentation
 */
import type { Locator, Page } from '@playwright/test';
import { installAlignmentProbe } from './alignment-probe';
import type { TimedLine } from './lyrics';

/** One recorded host mutation. */
export interface KaraokeSample {
  readonly state: string;
  readonly lineIndex: number;
  readonly wordIndex: number;
  /** The engine's own `data-position-ms`. */
  readonly positionMs: number;
  /** Reference transport position (ms, before the karaoke offset) at the mutation. */
  readonly referenceMs: number;
  /** `performance.now()` of the mutation. */
  readonly at: number;
}

/** Landing error of one word change. */
export interface Landing {
  readonly word: string;
  readonly errorMs: number;
}

interface SourceRecord {
  readonly node: AudioBufferSourceNode;
  readonly start: number;
  readonly offset: number;
  stopped: boolean;
}

interface ProbeWindow {
  __stemStudioSources?: SourceRecord[];
  __stemStudioKaraokeReference?: () => number;
}

/** Installs the source probe before any page script runs (call before `page.goto`). */
export async function installKaraokeProbe(page: Page): Promise<void> {
  await installAlignmentProbe(page);
  await page.addInitScript(referenceInitScript);
}

/** Records host mutations for `durationMs` while the page plays. */
export async function recordKaraoke(host: Locator, durationMs: number): Promise<KaraokeSample[]> {
  return host.evaluate(recordInPage, durationMs);
}

/**
 * Landing errors of every change to a new current word (state `singing`, valid indices); samples
 * whose word is unchanged are ignored.
 */
export function landingErrors(samples: readonly KaraokeSample[], lines: readonly TimedLine[], offsetMs: number): Landing[] {
  const out: Landing[] = [];
  let previous = '';
  for (const sample of samples) {
    const key = `${String(sample.lineIndex)}:${String(sample.wordIndex)}`;
    const word = key === previous ? undefined : singingWord(sample, lines);
    if (word !== undefined) {
      out.push({ word: word.text, errorMs: sample.referenceMs - offsetMs - word.startMs });
    }
    previous = key;
  }
  return out;
}

function singingWord(sample: KaraokeSample, lines: readonly TimedLine[]): TimedLine['words'][number] | undefined {
  return sample.state === 'singing' ? lines[sample.lineIndex]?.words[sample.wordIndex] : undefined;
}

/** Largest absolute landing error (0 when there are none). */
export function maxLandingErrorMs(landings: readonly Landing[]): number {
  return landings.reduce((max, landing) => Math.max(max, Math.abs(landing.errorMs)), 0);
}

/** Largest gap between `data-position-ms` and the reference (offset applied) over all samples. */
export function maxClockDriftMs(samples: readonly KaraokeSample[], offsetMs: number): number {
  return samples.reduce((max, s) => Math.max(max, Math.abs(s.referenceMs - offsetMs - s.positionMs)), 0);
}

// referenceInitScript and recordInPage are serialised into the page by Playwright: they must not
// reference anything outside their own bodies (types are erased and therefore allowed).
function referenceInitScript(): void {
  const liveSource = (): SourceRecord | undefined => {
    const all = (window as ProbeWindow).__stemStudioSources ?? [];
    return all.filter((r) => !r.stopped && (r.node.buffer?.duration ?? 0) >= 1).at(-1);
  };
  const audioNow = (ctx: AudioContext): number => {
    const ts = ctx.getOutputTimestamp();
    const context = ts.contextTime ?? 0;
    const perf = ts.performanceTime ?? 0;
    return context > 0 && perf > 0 ? context + (performance.now() - perf) / 1000 : ctx.currentTime;
  };
  const wrap = (node: AudioBufferSourceNode, position: number): number => {
    const length = node.loopEnd - node.loopStart;
    const looping = node.loop && length > 0 && position >= node.loopEnd;
    return looping ? node.loopStart + ((position - node.loopStart) % length) : position;
  };
  (window as ProbeWindow).__stemStudioKaraokeReference = (): number => {
    const source = liveSource();
    if (source === undefined) {
      return Number.NaN;
    }
    const ctx = source.node.context as AudioContext;
    const latency = (ctx as { outputLatency?: number }).outputLatency ?? 0;
    return Math.round(1000 * (wrap(source.node, source.offset + (audioNow(ctx) - source.start)) - latency));
  };
}

function recordInPage(host: Element, durationMs: number): Promise<KaraokeSample[]> {
  const reference = (window as ProbeWindow).__stemStudioKaraokeReference ?? ((): number => Number.NaN);
  const read = (name: string): number => Number(host.getAttribute(name) ?? Number.NaN);
  return new Promise((resolve) => {
    const samples: KaraokeSample[] = [];
    const observer = new MutationObserver(() => {
      const state = host.getAttribute('data-state') ?? '';
      samples.push({ state, lineIndex: read('data-line-index'), wordIndex: read('data-word-index'), positionMs: read('data-position-ms'), referenceMs: reference(), at: performance.now() });
    });
    observer.observe(host, { attributes: true, attributeFilter: ['data-word-index', 'data-line-index', 'data-state'] });
    window.setTimeout(() => {
      observer.disconnect();
      resolve(samples);
    }, durationMs);
  });
}
