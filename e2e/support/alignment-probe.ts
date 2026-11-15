/**
 * Sample-alignment probe for the mixer (feature 002, SC-004, FR-014).
 *
 * An init script wraps `AudioBufferSourceNode.prototype.start/stop` and records, per source, the
 * effective start time on the context clock, the buffer offset and the node itself (whose `loop`,
 * `loopStart` and `loopEnd` are read at measurement time). Contract 2.0.0 requires every audible
 * source to follow one transport anchor: position = offset + (t - start), wrapped into the loop.
 * Two sources are aligned when their anchors `offset - start` agree modulo the loop length.
 *
 * @packageDocumentation
 */
import type { Page } from '@playwright/test';

/** Result of one alignment measurement. */
export interface AlignmentSnapshot {
  /** Running stem sources (buffers of at least `minBufferSeconds`). */
  readonly sources: number;
  /** Completed loop repetitions of the most recently started source (0 when not looping). */
  readonly loops: number;
  /** Largest anchor difference between any running source and the first one, in ms. */
  readonly maxErrorMs: number;
}

interface SourceRecord {
  readonly node: AudioBufferSourceNode;
  readonly start: number;
  readonly offset: number;
  stopped: boolean;
}

interface ProbeWindow {
  __stemStudioSources?: SourceRecord[];
}

/** Installs the probe before any page script runs (call before `page.goto`). */
export async function installAlignmentProbe(page: Page): Promise<void> {
  await page.addInitScript(alignmentInitScript);
}

/** Measures the alignment of the running stem sources; metronome clicks are shorter and ignored. */
export async function alignmentSnapshot(page: Page, minBufferSeconds = 1): Promise<AlignmentSnapshot> {
  return page.evaluate(measureAlignment, minBufferSeconds);
}

// alignmentInitScript and measureAlignment are serialised into the page by Playwright: they must
// not reference anything outside their own bodies (types are erased and therefore allowed).
function alignmentInitScript(): void {
  const records: SourceRecord[] = [];
  (window as ProbeWindow).__stemStudioSources = records;
  type Method = (...args: unknown[]) => unknown;
  const methods = AudioBufferSourceNode.prototype as unknown as Record<'start' | 'stop', Method | undefined>;
  const { start, stop } = methods;
  if (start === undefined || stop === undefined) {
    return;
  }
  methods.start = function (this: AudioBufferSourceNode, ...args: unknown[]): unknown {
    const [when = 0, offset = 0] = args as [number?, number?];
    const record: SourceRecord = { node: this, start: Math.max(when, this.context.currentTime), offset, stopped: false };
    records.push(record);
    this.addEventListener('ended', () => {
      record.stopped = true;
    });
    return start.apply(this, args);
  };
  methods.stop = function (this: AudioBufferSourceNode, ...args: unknown[]): unknown {
    records.filter((r) => r.node === this).forEach((r) => (r.stopped = true));
    return stop.apply(this, args);
  };
}

function measureAlignment(minBufferSeconds: number): AlignmentSnapshot {
  const all = (window as ProbeWindow).__stemStudioSources ?? [];
  const running = all.filter((r) => !r.stopped && (r.node.buffer?.duration ?? 0) >= minBufferSeconds);
  const loopLength = (r: SourceRecord): number => (r.node.loop ? r.node.loopEnd - r.node.loopStart : 0);
  // Circular distance of two anchors modulo the loop length (plain distance when not looping).
  const distance = (a: number, b: number, length: number): number => {
    const d = length > 0 ? (((a - b) % length) + length) % length : Math.abs(a - b);
    return length > 0 ? Math.min(d, length - d) : d;
  };
  const [first, latest] = [running[0], running[running.length - 1]];
  if (first === undefined || latest === undefined) {
    return { sources: 0, loops: 0, maxErrorMs: Number.POSITIVE_INFINITY };
  }
  const errors = running.map((r) => distance(r.offset - r.start, first.offset - first.start, loopLength(r)));
  const length = loopLength(latest);
  const loops = length > 0 ? (latest.node.context.currentTime - latest.start) / length : 0;
  return { sources: running.length, loops, maxErrorMs: 1000 * Math.max(...errors) };
}
