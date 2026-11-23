/**
 * Offline scoring of a saved 015 take (contract 2.6.0 `scoreTake`, FR-046): fetches and decodes the take, runs the
 * shared decimator + YIN core and the 5-frame median over it, places its frames at `startSample` (44.1 kHz song
 * samples; takes are already latency-compensated, so no round trip) and scores every unit the take overlaps with
 * the live scoring core at the take's 012 transpose (`transposeSemitones`). The analysis yields to the event loop
 * after every ~1 s of take audio. No worklet is needed.
 *
 * @packageDocumentation
 */
import { DECIMATED_RATE } from '../pitch/decimate';
import { PitchTracker, YIN, type PitchFrame } from '../pitch/yin';
import { loadStems } from '../stem-loader';
import type { SingingUnit, SingingUnitScore, TakeScoreRequest, TakeScoreResult } from '../types';
import { MedianSmoother } from './median';
import { scoreUnit, summarise, type SingerTrack } from './score-core';
import { validateContour, validateTolerance, validateUnits } from './validate';

/** Song sample rate of `startSample` (Hz). */
const SONG_RATE = 44_100;
/** Silence padded before and after the take so its first and last 16 ms are analysed (s). */
const PAD_SECONDS = YIN.window / 2 / DECIMATED_RATE;
/** Take audio analysed between two yields to the event loop (s), so a long take never blocks the UI (review M5). */
const CHUNK_SECONDS = 1;
/** Largest 012 transpose (semitones). */
const MAX_TRANSPOSE = 12;

/** Engine parts the scoring uses. */
export interface TakeScoreDeps {
  /** Context used for decoding. */
  readonly context: BaseAudioContext;
  /** The engine's `fetch`. */
  readonly fetch: typeof fetch;
}

function fail(reason: string): never {
  throw new Error(`Invalid singing request: ${reason}`);
}

function validateTake(request: TakeScoreRequest): void {
  if (typeof request.takeUrl !== 'string' || request.takeUrl === '') {
    fail('takeUrl');
  }
  if (!Number.isInteger(request.startSample) || request.startSample < 0) {
    fail('startSample');
  }
}

/** @returns The validated transpose (integer -12..12; default 0). */
function validateTranspose(transpose: unknown): number {
  if (transpose === undefined) {
    return 0;
  }
  if (!Number.isInteger(transpose) || Math.abs(transpose as number) > MAX_TRANSPOSE) {
    fail('transposeSemitones');
  }
  return transpose as number;
}

/** Lets the event loop run (rendering, input, schedulers) between two analysis chunks. */
function yieldToEventLoop(): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, 0);
  });
}

/** Mono mix of the take, padded with {@link PAD_SECONDS} of silence on both sides. */
function monoPadded(buffer: AudioBuffer): Float32Array {
  const pad = Math.round(buffer.sampleRate * PAD_SECONDS);
  const mono = new Float32Array(buffer.length + 2 * pad);
  for (let c = 0; c < buffer.numberOfChannels; c += 1) {
    buffer.getChannelData(c).forEach((v, i) => {
      mono[pad + i] = Number(mono[pad + i]) + v / buffer.numberOfChannels;
    });
  }
  return mono;
}

/**
 * Singer frames of the take in song ms. The tracker runs over chunks of {@link CHUNK_SECONDS} with a yield between
 * them; its output does not depend on the block size, so the frames equal one synchronous pass.
 */
async function singerOf(buffer: AudioBuffer, startMs: number): Promise<SingerTrack> {
  const times: number[] = [];
  const cents: (number | null)[] = [];
  const median = new MedianSmoother();
  const onFrame = (frame: PitchFrame): void => {
    const centre = (frame.endSample - YIN.window / 2) / DECIMATED_RATE - PAD_SECONDS;
    if (frame.cents === null && (centre < PAD_SECONDS || centre > buffer.duration - PAD_SECONDS)) {
      return; // an edge window the padding silenced is no evidence about the singer
    }
    times.push(startMs + 1000 * centre);
    cents.push(median.next(frame.cents));
  };
  await trackInChunks(monoPadded(buffer), buffer.sampleRate, onFrame);
  return { times, cents };
}

/** Runs the tracker over `mono` in chunks of {@link CHUNK_SECONDS}, yielding to the event loop between chunks. */
async function trackInChunks(mono: Float32Array, rate: number, onFrame: (frame: PitchFrame) => void): Promise<void> {
  const tracker = new PitchTracker(rate);
  const chunk = Math.max(1, Math.round(rate * CHUNK_SECONDS));
  for (let i = 0; i < mono.length; i += chunk) {
    if (i > 0) {
      await yieldToEventLoop();
    }
    tracker.process(mono.subarray(i, i + chunk), onFrame);
  }
}

function covered(units: readonly SingingUnit[], startMs: number, endMs: number): SingingUnit[] {
  return units.filter((u) => u.endMs > startMs && u.startMs < endMs);
}

/**
 * @param request - Take URL, start, reference, units and tolerance.
 * @param deps - Decoding context and fetch.
 * @returns The score of every unit the take overlaps and that has enough voiced reference frames, and the summary.
 * @throws Error (rejects) for an invalid request or a take that cannot be fetched or decoded.
 */
export async function scoreTake(request: TakeScoreRequest, deps: TakeScoreDeps): Promise<TakeScoreResult> {
  const reference = validateContour(request.reference);
  validateUnits(request.units);
  validateTolerance(request.toleranceCents);
  validateTake(request);
  const transpose = validateTranspose(request.transposeSemitones);
  const buffers = await loadStems(deps.context, deps.fetch, [{ name: 'take', url: request.takeUrl }]);
  const buffer = buffers.get('take');
  if (buffer === undefined) {
    fail('take');
  }
  const units = scoreUnits({ request, reference, transpose }, buffer, await singerOf(buffer, startMsOf(request)));
  return { ...summarise(units, request.toleranceCents), latencySource: 'estimated', units };
}

/** Song ms of the take's first sample. */
function startMsOf(request: TakeScoreRequest): number {
  return (1000 * request.startSample) / SONG_RATE;
}

/** The validated request with its reference frames and transpose. */
interface TakeTarget {
  readonly request: TakeScoreRequest;
  readonly reference: Int16Array;
  readonly transpose: number;
}

/** Scores every unit the decoded take overlaps; units without enough voiced frames are left out. */
function scoreUnits(target: TakeTarget, buffer: AudioBuffer, singer: SingerTrack): SingingUnitScore[] {
  const { request, reference, transpose } = target;
  const startMs = startMsOf(request);
  return covered(request.units, startMs, startMs + 1000 * buffer.duration)
    .map((unit) => ({
      index: unit.index,
      score: scoreUnit({ reference, unit, toleranceCents: request.toleranceCents, transpose }, singer),
      pass: 1,
    }))
    .filter((unit) => unit.score !== null);
}
