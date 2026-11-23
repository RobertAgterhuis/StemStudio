import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createEngine, type AudioEngine, type SingingFeedbackRequest } from '../../src/index';
import type { DrumHitEvent, KaraokeLyrics } from '../../src/types';
import { PITCH_PROCESSOR_NAME } from '../../src/pitch/worklet';
import { FakeStretchStage } from '../../src/stretch/fake-stage';
import { FakeContext2D, stubCanvas } from '../mocks/canvas';
import { FakeDotNetRef } from '../mocks/dotnet-ref';
import { FetchStub } from '../mocks/fetch';
import { stubHostLayout } from '../mocks/engine-rig';
import { FakeMediaDevices, RecordingContext, stubSecureWorklets } from '../mocks/media';
import { GRID, addHost, cycleChart } from '../mocks/views-rig';

/**
 * Contract 2.6.0 / research R-12 frame budget: every view attached and playing at once (karaoke banner with chords,
 * chord lane, hit lanes, pitch view with live singing) must draw a frame in <= 4 ms on average, with counted canvas
 * primitives. jsdom is slower than a browser, so passing here leaves headroom; SC-007 is measured in Chromium.
 */

const FRAME_MS = 1000 / 60;
const FRAMES = 300;
const BUDGET_MS = 4;
/** R-12: "a few hundred primitives" per canvas view; two canvases. */
const MAX_PRIMITIVES = 1000;

interface BenchRig {
  readonly ctx: RecordingContext;
  readonly engine: AudioEngine;
  readonly step: () => void;
}

/** 60 s of lyrics, one 2 s line of four words. */
function lyrics(): KaraokeLyrics {
  const lines = Array.from({ length: 30 }, (_, i) => {
    const start = i * 2000;
    const words = Array.from({ length: 4 }, (__, w) => ({ text: `w${String(w)}`, startMs: start + w * 500, endMs: start + w * 500 + 450 }));
    return { startMs: start, endMs: start + 2000, words };
  });
  return { lines };
}

/** Dense groove: hihat eighths, kick and snare on the beats, a crash per bar, over 60 s at 120 bpm. */
function hits(): DrumHitEvent[] {
  const all: DrumHitEvent[] = [];
  for (let t = 0; t < 60_000; t += 250) {
    all.push({ timeMs: t, piece: 'hihat', strength: 0.6 });
    if (t % 500 === 0) {
      all.push({ timeMs: t, piece: t % 1000 === 0 ? 'kick' : 'snare', strength: 0.9 });
    }
    if (t % 2000 === 0) {
      all.push({ timeMs: t, piece: 'cymbals', strength: 1 });
    }
  }
  return all;
}

/** Collects the requested animation frames; the returned step runs them once. */
function stubFrames(): () => void {
  const frames: FrameRequestCallback[] = [];
  vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => frames.push(cb));
  vi.stubGlobal('cancelAnimationFrame', vi.fn());
  return () => {
    frames.splice(0).forEach((cb) => {
      cb(0);
    });
  };
}

/** 60 s melody: 0.5 s notes stepping through an octave, every third one a rest. */
const MELODY = Array.from({ length: 6000 }, (_, i) => (Math.floor(i / 50) % 3 === 2 ? 0 : 6000 + 100 * (Math.floor(i / 50) % 12)));

const SINGING: SingingFeedbackRequest = {
  pitchViewElementId: 'pitch-view',
  reference: { hopMs: 10, frames: MELODY },
  units: Array.from({ length: 30 }, (_, i) => ({ index: i, startMs: i * 2000, endMs: i * 2000 + 2000 })),
  toleranceCents: 50,
  roundTripMs: 0,
  latencyMethod: 'loopback',
  reducedMotion: false,
};

async function makeRig(): Promise<BenchRig> {
  const step = stubFrames();
  const ctx = new RecordingContext();
  ctx.decodeDurations.set(100, 60);
  const engine = createEngine(new FakeDotNetRef(), {
    audioContextFactory: () => ctx.asAudioContext(),
    fetch: new FetchStub().reply('/vocals', { bytes: 100 }).fetch,
    mediaDevices: new FakeMediaDevices(),
    timeStretchFactory: (context) => Promise.resolve(new FakeStretchStage(context)),
    pitchWorkletUrl: '/pitch.js',
  });
  await engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: GRID });
  await engine.openInput({ deviceId: 'usb', mode: 'mono-1' });
  return { ctx, engine, step };
}

async function attachAll(rig: BenchRig): Promise<void> {
  ['karaoke-banner', 'chord-lane', 'hits', 'pitch-view'].forEach(addHost);
  const { engine } = rig;
  engine.setKaraokeChords(cycleChart());
  engine.attachKaraoke('karaoke-banner', lyrics(), { offsetMs: 0, reducedMotion: false });
  engine.attachChordLane('chord-lane', cycleChart(), { reducedMotion: false });
  engine.attachHitLanes('hits', hits(), { reducedMotion: false });
  await engine.startSingingFeedback(SINGING);
}

/** Posts the pitch frames of one display frame (10 ms hops) up to the context time. */
function sing(rig: BenchRig, fromS: number, toS: number): void {
  const node = rig.ctx.workletNodes.filter((n) => n.name === PITCH_PROCESSOR_NAME).at(-1);
  for (let t = fromS; t < toS; t += 0.01) {
    node?.port.reply({ t, cents: 6000 + 40 * Math.sin(t * 7), clarity: 1 });
  }
}

function primitives(): number {
  return FakeContext2D.instances.reduce((sum, c) => sum + c.calls.length, 0);
}

interface Measurement {
  readonly average: number;
  readonly p95: number;
  readonly worstPrimitives: number;
}

/** Plays {@link FRAMES} display frames with live pitch frames and times only the drawing. */
function measure(rig: BenchRig): Measurement {
  const timings: number[] = [];
  let worstPrimitives = 0;
  for (let i = 0; i < FRAMES; i += 1) {
    const from = rig.ctx.currentTime;
    rig.ctx.currentTime = from + FRAME_MS / 1000;
    sing(rig, from, rig.ctx.currentTime);
    const drawn = primitives();
    const start = performance.now();
    rig.step();
    timings.push(performance.now() - start);
    worstPrimitives = Math.max(worstPrimitives, primitives() - drawn);
  }
  const sorted = [...timings].sort((a, b) => a - b);
  const average = timings.reduce((a, b) => a + b, 0) / timings.length;
  return { average, p95: sorted[Math.floor(0.95 * sorted.length)] ?? 0, worstPrimitives };
}

beforeEach(() => {
  document.body.replaceChildren();
  stubSecureWorklets();
  stubHostLayout();
  stubCanvas();
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('frame budget of all views together (R-12)', () => {
  it(`draws a frame of every view in <= ${String(BUDGET_MS)} ms on average with counted primitives`, async () => {
    const rig = await makeRig();
    await attachAll(rig);
    rig.ctx.currentTime = 0;
    await rig.engine.play();
    rig.step();
    const { average, p95, worstPrimitives } = measure(rig);
    console.info(`[frame-budget] all views: ${average.toFixed(3)} ms avg, ${p95.toFixed(3)} ms p95, ${String(worstPrimitives)} canvas primitives worst`);
    expect(document.querySelectorAll('.karaoke-chord').length).toBeGreaterThan(0);
    expect(document.querySelectorAll('.chord-lane-item').length).toBeGreaterThan(0);
    expect(worstPrimitives).toBeGreaterThan(0);
    expect(worstPrimitives).toBeLessThanOrEqual(MAX_PRIMITIVES);
    expect(average).toBeLessThanOrEqual(BUDGET_MS);
  });
});
