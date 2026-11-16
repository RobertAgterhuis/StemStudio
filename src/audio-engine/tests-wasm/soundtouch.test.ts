import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { beforeAll, describe, expect, it } from 'vitest';
import { instantiateSoundTouch, SoundTouchStream, type SoundTouchExports } from '../src/stretch/abi';
import { RENDER_QUANTUM_FRAMES, SOUNDTOUCH_SETTINGS, STRETCH_DELAY_FRAMES } from '../src/stretch/constants';
import { PitchQueue, StretchCore } from '../src/stretch/core';
import { contentOffsetFrames } from '../src/stretch/params';
import { bursts, envelopeOnsets, noise, peakFrequency, sine } from './signal';

/**
 * Real `soundtouch.wasm` through the same `abi.ts` the processor uses (audio-engine.md 2.3.0 "Testing", SC-002,
 * SC-004). Skipped with a message when the module was not built; `STEMSTUDIO_REQUIRE_WASM=1` turns that into a failure.
 */
const MODULE_PATH =
  process.env.STEMSTUDIO_SOUNDTOUCH_WASM ??
  fileURLToPath(new URL('../../StemStudio.Web/wwwroot/js/soundtouch/soundtouch.wasm', import.meta.url));
const AVAILABLE = existsSync(MODULE_PATH);
const RATE = 44100;
const Q = RENDER_QUANTUM_FRAMES;
const LIMIT = STRETCH_DELAY_FRAMES - 256;
const FACTORS = [0.42, 0.5, 0.71, 1, 1.41, 2, 2.83, 4];

if (!AVAILABLE) {
  const message = `soundtouch.wasm not found at ${MODULE_PATH}: build it with scripts/build-soundtouch.sh (WASM suite skipped)`;
  if (process.env.STEMSTUDIO_REQUIRE_WASM === '1') {
    throw new Error(message);
  }
  console.warn(message);
}

let abi: SoundTouchExports;

/** Raw put/receive loop over one instance (no FIFO): records the left output and the largest put-minus-received lag. */
class RawStream {
  private readonly handle: number;
  private readonly inPtr: number;
  private readonly outPtr: number;
  public maxLag = 0;
  private put = 0;
  private received = 0;

  public constructor(pitch: number) {
    this.handle = abi.st_create(RATE, 2);
    SOUNDTOUCH_SETTINGS.forEach(([id, value]) => abi.st_set_setting(this.handle, id, value));
    abi.st_set_pitch(this.handle, pitch);
    this.inPtr = abi.st_malloc(Q * 8);
    this.outPtr = abi.st_malloc(4096 * 8);
  }

  public setPitch(pitch: number): void {
    abi.st_set_pitch(this.handle, pitch);
  }

  public run(input: Float32Array, onBlock: (block: number) => void = () => undefined): Float32Array {
    const output: number[] = [];
    for (let block = 0; (block + 1) * Q <= input.length; block++) {
      onBlock(block);
      this.feed(input.subarray(block * Q, (block + 1) * Q));
      this.drain(output);
      this.maxLag = Math.max(this.maxLag, this.put - this.received);
    }
    return Float32Array.from(output);
  }

  public dispose(): void {
    abi.st_destroy(this.handle);
    abi.st_free(this.inPtr);
    abi.st_free(this.outPtr);
  }

  private feed(block: Float32Array): void {
    const view = new Float32Array(abi.memory.buffer, this.inPtr, Q * 2);
    block.forEach((value, i) => {
      view[2 * i] = value;
      view[2 * i + 1] = value;
    });
    abi.st_put(this.handle, this.inPtr, Q);
    this.put += Q;
  }

  private drain(output: number[]): void {
    for (let frames = 4096; frames === 4096; ) {
      frames = abi.st_receive(this.handle, this.outPtr, 4096);
      const view = new Float32Array(abi.memory.buffer, this.outPtr, frames * 2);
      for (let i = 0; i < frames; i++) {
        output.push(Number(view[2 * i]));
      }
      this.received += frames;
    }
  }
}

function stretched(pitch: number, input: Float32Array): Float32Array {
  const stream = new RawStream(pitch);
  const output = stream.run(input);
  stream.dispose();
  return output;
}

/**
 * Mean of `output onset - input onset` over bursts found in both signals (frames): the content of input frame `n`
 * appears near SoundTouch output frame `n + c(p)`, which is why the heard latency is `(D + c(p)) / sampleRate`.
 */
function meanContentOffset(pitch: number): number {
  const { signal, onsets } = bursts(54 * 13230, 13230, 882);
  const inputOnsets = envelopeOnsets(signal);
  const outputOnsets = envelopeOnsets(stretched(pitch, signal));
  expect(inputOnsets).toHaveLength(onsets.length);
  const offsets = inputOnsets.flatMap((n) => {
    const m = outputOnsets.find((candidate) => Math.abs(candidate - n) < 0.1 * RATE);
    return m === undefined ? [] : [m - n];
  });
  expect(offsets.length).toBeGreaterThan(45);
  return offsets.reduce((a, b) => a + b, 0) / offsets.length;
}

describe.skipIf(!AVAILABLE)('soundtouch.wasm (WASM ABI v1)', () => {
  beforeAll(async () => {
    abi = await instantiateSoundTouch(readFileSync(MODULE_PATH));
  });

  it('reports ABI version 1 and exports every ABI function', () => {
    expect(abi.st_abi_version()).toBe(1);
    expect(abi.memory.buffer.byteLength).toBe(4 * 1024 * 1024);
    expect(abi.st_create(0, 2)).toBe(0);
    expect(abi.st_create(RATE, 3)).toBe(0);
  });

  it.each([0.5, 1, 2])('shifts a 440 Hz sine by pitch factor %f within 1 % (SC-002)', (pitch) => {
    const output = stretched(pitch, sine(440, 3 * RATE, RATE));
    const frequency = peakFrequency(output.subarray(16384), RATE);
    expect(Math.abs(frequency / (440 * pitch) - 1)).toBeLessThan(0.01);
  });

  it('passes audio through at factor 1 after the fixed delay (max error < 1e-4)', () => {
    const core = new StretchCore(SoundTouchStream.create(abi, RATE), new PitchQueue(), STRETCH_DELAY_FRAMES);
    const input = noise(RATE, 9).map((v) => 0.5 * v);
    const heard: number[] = [];
    for (let block = 0; (block + 1) * Q <= input.length; block++) {
      const left = input.slice(block * Q, (block + 1) * Q);
      const out = { left: new Float32Array(Q), right: new Float32Array(Q) };
      core.process({ left, right: left }, out, block * Q);
      heard.push(...out.left);
    }
    core.dispose();
    let maxError = 0;
    for (let j = STRETCH_DELAY_FRAMES + 8192; j < heard.length; j++) {
      maxError = Math.max(maxError, Math.abs(Number(heard[j]) - Number(input[j - STRETCH_DELAY_FRAMES])));
    }
    expect(maxError).toBeLessThan(1e-4);
  });

  it.each(FACTORS)('has an initial latency <= D - 256 at factor %f', (pitch) => {
    const handle = abi.st_create(RATE, 2);
    SOUNDTOUCH_SETTINGS.forEach(([id, value]) => abi.st_set_setting(handle, id, value));
    abi.st_set_pitch(handle, pitch);
    const latency = abi.st_initial_latency(handle);
    abi.st_destroy(handle);
    expect(latency).toBeGreaterThan(0);
    expect(latency).toBeLessThanOrEqual(LIMIT);
  });

  it.each(FACTORS)('keeps the output lag <= D - 256 at constant factor %f', (pitch) => {
    const stream = new RawStream(pitch);
    stream.run(noise(10 * RATE, 5));
    stream.dispose();
    expect(stream.maxLag).toBeLessThanOrEqual(LIMIT);
  });

  it('keeps the output lag <= D - 256 over 100 jumps 0.42 <-> 4 (no drift)', () => {
    const stream = new RawStream(0.42);
    const blocksPerJump = Math.round((0.25 * RATE) / Q);
    stream.run(noise(101 * blocksPerJump * Q, 11), (block) => {
      if (block > 0 && block % blocksPerJump === 0) {
        stream.setPitch((block / blocksPerJump) % 2 === 0 ? 0.42 : 4);
      }
    });
    stream.dispose();
    console.info(`max output lag over 100 jumps: ${String(stream.maxLag)} frames`);
    expect(stream.maxLag).toBeLessThanOrEqual(LIMIT);
  });

  it.each([0.5, 2])('has a mean content offset at factor %f within 25 ms of c(p) (ADR-021 P6)', (pitch) => {
    const mean = meanContentOffset(pitch);
    console.info(`content offset at ${String(pitch)}: ${(mean / 44.1).toFixed(1)} ms, c(p) ${(contentOffsetFrames(pitch) / 44.1).toFixed(1)} ms`);
    expect(Math.abs(mean - contentOffsetFrames(pitch))).toBeLessThan(0.025 * RATE);
  });

  it.each([0.42, 4])('runs stereo 44.1 kHz at factor %f at least 10x faster than real time (SC-004)', (pitch) => {
    const seconds = 20;
    const input = noise(seconds * RATE, 13).map((v, i) => 0.3 * v + 0.3 * Math.sin((2 * Math.PI * 220 * i) / RATE));
    const started = performance.now();
    stretched(pitch, input);
    const elapsed = (performance.now() - started) / 1000;
    console.info(`throughput at ${String(pitch)}: ${(seconds / elapsed).toFixed(0)}x real time`);
    expect(seconds / elapsed).toBeGreaterThanOrEqual(10);
  });
});
