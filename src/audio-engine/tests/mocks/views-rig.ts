import { vi } from 'vitest';
import { FrameLoop } from '../../src/frame-loop';
import { TimelineClock } from '../../src/timeline/clock';
import type { BeatGrid, ChordChartData, ChordQuality, ChordSymbolEvent } from '../../src/types';
import { FakeFrames } from './frames';
import { stubResizeObserver } from './layout';
import { makeTransport, type TransportRig } from './transport-rig';

/** Host width of the stubbed layout (px). */
export const HOST_WIDTH = 800;

/** Mutable host size used by the stubbed `getBoundingClientRect`. */
export const hostSize = { width: HOST_WIDTH, height: 120 };

/** Shared parts of a view rig: transport, frames, loop and the timeline clock. */
export interface ViewRig extends TransportRig {
  readonly frames: FakeFrames;
  readonly loop: FrameLoop;
  readonly clock: TimelineClock;
  readonly playing: () => boolean;
}

/** Builds a {@link ViewRig} over a 60 s track and stubs layout + ResizeObserver. */
export function makeViewRig(): ViewRig {
  hostSize.width = HOST_WIDTH;
  hostSize.height = 120;
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(
    () => ({ left: 0, top: 0, width: hostSize.width, height: hostSize.height, right: hostSize.width, bottom: hostSize.height, x: 0, y: 0 }) as DOMRect,
  );
  stubResizeObserver();
  const rig = makeTransport();
  const frames = new FakeFrames();
  const loop = new FrameLoop({ frames, document });
  const clock = new TimelineClock({ context: rig.ctx.asAudioContext(), transport: rig.transport, now: () => 0 });
  return { ...rig, frames, loop, clock, playing: () => rig.transport.state === 'playing' };
}

/** Adds an empty host `<div id=...>` to the document body. */
export function addHost(id: string): HTMLElement {
  const host = document.createElement('div');
  host.id = id;
  document.body.append(host);
  return host;
}

/** 4/4 grid at 120 BPM over 60 s: bars of 2 s (downbeats 0, 2, ..., 60). */
export const GRID: BeatGrid = {
  bpm: 120,
  beats: Array.from({ length: 121 }, (_, i) => i * 0.5),
  downbeats: Array.from({ length: 31 }, (_, i) => i * 2),
  beatsPerBar: 4,
  confidence: 0.9,
  reliable: true,
};

const CYCLE: readonly [number, ChordQuality, number | null][] = [
  [0, 'maj', null],
  [9, 'min', null],
  [5, 'maj', null],
  [7, '7', 11],
];

function cycleEntry(index: number): readonly [number, ChordQuality, number | null] {
  return CYCLE[index % CYCLE.length] ?? [0, 'maj', null];
}

/** `N.C.` 0-1000, then C, Am, F, G7/B repeating every 2 s up to 59 s, `N.C.` to 60 s (31 segments). */
export function cycleChart(keyTonic: number | null = 0): ChordChartData {
  const segments: ChordSymbolEvent[] = [{ startMs: 0, endMs: 1000, root: null, quality: null, bass: null }];
  for (let start = 1000; start < 59_000; start += 2000) {
    const [root, quality, bass] = cycleEntry(segments.length - 1);
    segments.push({ startMs: start, endMs: start + 2000, root, quality, bass });
  }
  segments.push({ startMs: 59_000, endMs: 60_000, root: null, quality: null, bass: null });
  return { segments, keyTonic, keyMode: keyTonic === null ? null : 'major' };
}

/** While playing: moves the context so that track time `ms` is heard, then draws one frame. */
export function frameAt(rig: ViewRig, ms: number): void {
  rig.ctx.currentTime = 0.1 + ms / 1000;
  rig.frames.step(ms);
}

/** While stopped/paused: seeks to `ms` and draws one frame. */
export function showAt(rig: ViewRig, ms: number): void {
  rig.transport.seek(ms / 1000);
  rig.frames.step(ms);
}
