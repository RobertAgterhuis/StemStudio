import { afterEach, describe, expect, it } from 'vitest';
import { STRETCH_DELAY_FRAMES, STRETCH_OVERFLOW_FRAMES } from '../../src/stretch/constants';
import { DelayFifo, MAX_WRITE_FRAMES, interleave } from '../../src/stretch/fifo';

const QUANTUM = 128;

/** Interleaved stereo frames whose left value is `first + i` and right value is `-(first + i)`. */
function ramp(first: number, frames: number): Float32Array {
  const data = new Float32Array(frames * 2);
  for (let i = 0; i < frames; i++) {
    data[2 * i] = first + i;
    data[2 * i + 1] = -(first + i);
  }
  return data;
}

function readQuantum(fifo: DelayFifo): { left: Float32Array; right: Float32Array } {
  const left = new Float32Array(QUANTUM);
  const right = new Float32Array(QUANTUM);
  fifo.read(left, right);
  return { left, right };
}

/** Deterministic pseudo-random numbers 0..1 (LCG). */
function prng(seed: number): () => number {
  let state = seed;
  return () => {
    state = (state * 1664525 + 1013904223) % 2 ** 32;
    return state / 2 ** 32;
  };
}

const RealFloat32Array = globalThis.Float32Array;

afterEach(() => {
  globalThis.Float32Array = RealFloat32Array;
});

describe('DelayFifo fixed delay (FR-003, SC-001)', () => {
  it('starts with D frames of silence', () => {
    const fifo = new DelayFifo(256);
    expect(fifo.size).toBe(256);
    const { left, right } = readQuantum(fifo);
    expect([...left].every((v) => v === 0)).toBe(true);
    expect([...right].every((v) => v === 0)).toBe(true);
    expect(fifo.size).toBe(128);
  });

  it('plays input frame n at output frame n + D with a jittering producer', () => {
    const delay = STRETCH_DELAY_FRAMES;
    const fifo = new DelayFifo(delay);
    const random = prng(7);
    let produced = 0;
    const heard: number[] = [];
    for (let q = 0; q < 400; q++) {
      const consumed = (q + 1) * QUANTUM;
      const lag = Math.floor(random() * 4736);
      const target = Math.max(produced, consumed - lag);
      fifo.write(ramp(produced + 1, target - produced), target - produced);
      produced = target;
      heard.push(...readQuantum(fifo).left);
    }
    expect(fifo.underruns).toBe(0);
    heard.forEach((value, j) => {
      expect(value).toBe(j < delay ? 0 : j - delay + 1);
    });
  });

  it('deinterleaves left and right', () => {
    const fifo = new DelayFifo(0);
    fifo.write(ramp(1, QUANTUM), QUANTUM);
    const { left, right } = readQuantum(fifo);
    expect([left[0], right[0], left[127], right[127]]).toEqual([1, -1, 128, -128]);
  });

  it('writes only the given number of frames of a larger buffer', () => {
    const fifo = new DelayFifo(0);
    fifo.write(ramp(1, 512), 3);
    expect(fifo.size).toBe(3);
  });
});

describe('DelayFifo underrun and overflow', () => {
  it('outputs silence for missing frames and counts them', () => {
    const fifo = new DelayFifo(0);
    fifo.write(ramp(1, 100), 100);
    const { left } = readQuantum(fifo);
    expect(left[99]).toBe(100);
    expect([...left.slice(100)].every((v) => v === 0)).toBe(true);
    expect(fifo.underruns).toBe(28);
    readQuantum(fifo);
    expect(fifo.underruns).toBe(156);
    expect(fifo.size).toBe(0);
  });

  it('drops the oldest surplus above D + 1024 back to D and counts the dropped frames', () => {
    const delay = 512;
    const fifo = new DelayFifo(delay);
    fifo.write(ramp(1, STRETCH_OVERFLOW_FRAMES), STRETCH_OVERFLOW_FRAMES);
    expect(fifo.size).toBe(delay + STRETCH_OVERFLOW_FRAMES);
    expect(fifo.underruns).toBe(0);
    fifo.write(ramp(STRETCH_OVERFLOW_FRAMES + 1, 1), 1);
    expect(fifo.size).toBe(delay);
    expect(fifo.underruns).toBe(STRETCH_OVERFLOW_FRAMES + 1);
    const { left } = readQuantum(fifo);
    expect(left[0]).toBe(STRETCH_OVERFLOW_FRAMES + 1 - delay + 1);
  });

  it('survives writes far above the overflow limit', () => {
    const fifo = new DelayFifo(256);
    fifo.write(ramp(1, MAX_WRITE_FRAMES), MAX_WRITE_FRAMES);
    fifo.write(ramp(MAX_WRITE_FRAMES + 1, MAX_WRITE_FRAMES), MAX_WRITE_FRAMES);
    expect(fifo.size).toBe(256);
    const { left } = readQuantum(fifo);
    expect(left[0]).toBe(2 * MAX_WRITE_FRAMES - 256 + 1);
  });
});

describe('DelayFifo reset', () => {
  it('drops buffered audio and refills D frames of silence; the counter is kept', () => {
    const fifo = new DelayFifo(256);
    fifo.write(ramp(1, 300), 300);
    readQuantum(fifo);
    readQuantum(fifo);
    readQuantum(fifo);
    readQuantum(fifo);
    readQuantum(fifo);
    const before = fifo.underruns;
    fifo.reset();
    expect(fifo.size).toBe(256);
    expect(fifo.underruns).toBe(before);
    const { left } = readQuantum(fifo);
    expect([...left].every((v) => v === 0)).toBe(true);
  });
});

describe('interleave', () => {
  it('writes left/right pairs into the target', () => {
    const target = new Float32Array(8);
    interleave(new Float32Array([1, 2, 3]), new Float32Array([-1, -2, -3]), target);
    expect([...target]).toEqual([1, -1, 2, -2, 3, -3, 0, 0]);
  });
});

describe('no allocation per call', () => {
  it('allocates no typed array in write, read, reset or interleave', () => {
    const fifo = new DelayFifo(STRETCH_DELAY_FRAMES);
    const chunk = ramp(1, QUANTUM);
    const left = new Float32Array(QUANTUM);
    const right = new Float32Array(QUANTUM);
    const target = new Float32Array(2 * QUANTUM);
    let constructed = 0;
    globalThis.Float32Array = new Proxy(RealFloat32Array, {
      construct(type, args: unknown[]) {
        constructed += 1;
        return Reflect.construct(type, args) as object;
      },
    });
    for (let i = 0; i < 100; i++) {
      fifo.write(chunk, QUANTUM);
      fifo.read(left, right);
      interleave(left, right, target);
    }
    fifo.reset();
    expect(constructed).toBe(0);
  });
});
