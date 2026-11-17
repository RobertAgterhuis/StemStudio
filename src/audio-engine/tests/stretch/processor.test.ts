import { afterEach, describe, expect, it, vi } from 'vitest';
import { SOUNDTOUCH_SETTINGS, STRETCH_DELAY_FRAMES, STRETCH_PROCESSOR_NAME } from '../../src/stretch/constants';
import { instantiateSoundTouch, type AbiLoader } from '../../src/stretch/abi';
import { registerTimeStretchProcessor, type StretchProcessor } from '../../src/stretch/processor';
import { FakeSoundTouch } from '../mocks/fake-soundtouch';
import { FakeWorkletScope, type FakeProcessorPort } from '../mocks/worklet-scope';

const Q = 128;

interface Rig {
  readonly scope: FakeWorkletScope;
  readonly node: StretchProcessor;
  readonly port: FakeProcessorPort;
  readonly fake: FakeSoundTouch;
}

function rig(options: { fake?: FakeSoundTouch; load?: AbiLoader; delayFrames?: number } = {}): Rig {
  const fake = options.fake ?? new FakeSoundTouch();
  const scope = new FakeWorkletScope();
  scope.currentFrame = 44100;
  registerTimeStretchProcessor(scope, options.load ?? (() => Promise.resolve(fake)));
  const Processor = scope.registered.get(STRETCH_PROCESSOR_NAME);
  if (Processor === undefined) {
    throw new Error('not registered');
  }
  const node = new Processor({ processorOptions: { wasmBytes: new ArrayBuffer(8), delayFrames: options.delayFrames } });
  return { scope, node, port: node.port as unknown as FakeProcessorPort, fake };
}

const flush = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));

async function ready(options: Parameters<typeof rig>[0] = {}): Promise<Rig> {
  const r = rig(options);
  await flush();
  return r;
}

/** Input block whose frame values are `first + i` (left) and `-(first + i)` (right). */
function block(first: number): Float32Array[][] {
  const left = new Float32Array(Q).map((_, i) => first + i);
  const right = left.map((v) => -v);
  return [[left, right]];
}

function outputs(): Float32Array[][] {
  return [[new Float32Array(Q), new Float32Array(Q)]];
}

/** Runs one quantum with `input`, advancing `currentFrame`; returns the output and the return value. */
function quantum(r: Rig, input: Float32Array[][] = block(0)): { out: Float32Array[][]; alive: boolean } {
  const out = outputs();
  const alive = r.node.process(input, out);
  r.scope.currentFrame += Q;
  return { out, alive };
}

function left(out: Float32Array[][]): number[] {
  return [...(out[0]?.[0] ?? [])];
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('time-stretch processor: loading (FR-006)', () => {
  it('registers itself under the contract name', () => {
    const r = rig();
    expect(r.scope.registered.has('stemstudio-time-stretch')).toBe(true);
  });

  it('outputs silence until ready, then posts ready with latencyFrames', async () => {
    const r = rig();
    const before = quantum(r, block(1));
    expect(left(before.out).every((v) => v === 0)).toBe(true);
    expect(before.alive).toBe(true);
    expect(r.port.ofType('ready')).toEqual([]);
    await flush();
    expect(r.port.ofType('ready')).toEqual([{ type: 'ready', latencyFrames: STRETCH_DELAY_FRAMES }]);
  });

  it('creates one stereo instance at the scope sample rate and sends the R-04 settings in order', async () => {
    const r = await ready();
    expect(r.fake.created).toEqual([[44100, 2]]);
    expect(r.fake.settings).toEqual(SOUNDTOUCH_SETTINGS.map(([id, value]) => [id, value]));
  });

  it('uses a delay given in the processor options', async () => {
    const r = await ready({ delayFrames: 256 });
    expect(r.port.ofType('ready')).toEqual([{ type: 'ready', latencyFrames: 256 }]);
  });

  it.each([
    ['the module fails to instantiate', (): Promise<never> => Promise.reject(new Error('bad magic')), 'bad magic'],
    // eslint-disable-next-line @typescript-eslint/prefer-promise-reject-errors -- a non-Error rejection is the case under test
    ['the loader rejects with a non-error', (): Promise<never> => Promise.reject(42), '42'],
  ])('posts an error when %s', async (_name, load, message) => {
    const r = await ready({ load });
    expect(r.port.ofType('error')).toEqual([{ type: 'error', message }]);
    expect(left(quantum(r, block(1)).out).every((v) => v === 0)).toBe(true);
  });

  it('posts an error for a wrong ABI version', async () => {
    const fake = new FakeSoundTouch();
    fake.abiVersion = 2;
    const r = await ready({ fake });
    expect(r.port.ofType('error')).toEqual([{ type: 'error', message: 'soundtouch.wasm has ABI version 2, expected 1' }]);
  });

  it('posts an error when st_create fails', async () => {
    const fake = new FakeSoundTouch();
    fake.createResult = 0;
    const r = await ready({ fake });
    expect(r.port.ofType('error')).toEqual([{ type: 'error', message: 'st_create failed' }]);
  });

  it('posts an error when st_malloc fails', async () => {
    const fake = new FakeSoundTouch();
    fake.mallocResult = 0;
    const r = await ready({ fake });
    expect(r.port.ofType('error')).toEqual([{ type: 'error', message: 'st_malloc failed' }]);
  });

  it('allocates its WASM buffers before ready and never in process()', async () => {
    const r = await ready();
    const mallocs = r.fake.mallocs;
    expect(mallocs).toBeGreaterThan(0);
    for (let i = 0; i < 100; i++) {
      quantum(r, block(i * Q));
    }
    expect(r.fake.mallocs).toBe(mallocs);
  });
});

describe('time-stretch processor: fixed delay', () => {
  it('plays input frame n at output frame n + D through an identity SoundTouch with jitter', async () => {
    const fake = new FakeSoundTouch();
    let step = 0;
    fake.latency = 300;
    fake.jitter = () => (step++ % 3) * 50;
    const r = await ready({ fake, delayFrames: 512 });
    const heard: number[] = [];
    for (let q = 0; q < 20; q++) {
      heard.push(...left(quantum(r, block(q * Q + 1)).out));
    }
    heard.forEach((value, j) => {
      expect(value).toBe(j < 512 ? 0 : j - 512 + 1);
    });
  });

  it('treats a missing input as silence and copies a mono input to both channels', async () => {
    const r = await ready({ delayFrames: 0 });
    expect(left(quantum(r, [[]]).out).every((v) => v === 0)).toBe(true);
    expect(left(quantum(r, []).out).every((v) => v === 0)).toBe(true);
    const mono = [[new Float32Array(Q).fill(0.5)]];
    const { out } = quantum(r, mono);
    expect([out[0]?.[0]?.[0], out[0]?.[1]?.[0]]).toEqual([0.5, 0.5]);
  });

  it('receives a burst larger than one receive chunk completely', async () => {
    const fake = new FakeSoundTouch();
    fake.latency = 5000;
    const r = await ready({ fake, delayFrames: 6000 });
    for (let q = 0; q < 40; q++) {
      quantum(r, block(q * Q + 1));
    }
    fake.latency = 0;
    const heard: number[] = [];
    for (let q = 40; q < 60; q++) {
      heard.push(...left(quantum(r, block(q * Q + 1)).out));
    }
    heard.forEach((value, i) => {
      const j = 40 * Q + i;
      expect(value).toBe(j < 6000 ? 0 : j - 6000 + 1);
    });
  });

  it('survives a node without outputs', async () => {
    const r = await ready();
    expect(r.node.process(block(1), [])).toBe(true);
  });

  it('writes only the left channel when the output has one channel', async () => {
    const r = await ready({ delayFrames: 0 });
    const out = [[new Float32Array(Q)]];
    r.node.process(block(1), out);
    expect(out[0]?.[0]?.[0]).toBe(1);
  });

  it('reports underruns as stats at most 4 times per second', async () => {
    const fake = new FakeSoundTouch();
    fake.latency = 1_000_000;
    const r = await ready({ fake, delayFrames: 0 });
    for (let q = 0; q < 690; q++) {
      quantum(r);
    }
    const stats = r.port.ofType('stats');
    expect(stats.length).toBeGreaterThanOrEqual(7);
    expect(stats.length).toBeLessThanOrEqual(9);
    expect(stats[0]).toEqual({ type: 'stats', underruns: Q });
    const counts = stats.map((s) => Number(s.underruns));
    expect(counts).toEqual([...counts].sort((a, b) => a - b));
  });

  it('does not post stats while nothing changes', async () => {
    const r = await ready({ delayFrames: 0 });
    for (let q = 0; q < 100; q++) {
      quantum(r);
    }
    expect(r.port.ofType('stats')).toEqual([]);
  });
});

describe('time-stretch processor: pitch messages (ADR-021 P7 midpoint rule)', () => {
  it('applies a pitch when the midpoint of the buffered input reaches atFrame, also inside a block', async () => {
    const fake = new FakeSoundTouch();
    fake.latency = 1000;
    const r = await ready({ fake, delayFrames: 2048 });
    quantum(r);
    const first = r.scope.currentFrame - Q;
    r.port.send({ type: 'pitch', value: 2, atFrame: first + 1960 });
    for (let q = 0; q < 30; q++) {
      quantum(r);
    }
    // Block 19 starts with 2432 frames put and 1432 received: (2432 + k + 1432) / 2 >= 1960 at k = 56.
    expect(fake.pitches).toEqual([{ value: 2, inFrames: 2488 }]);
  });

  it('applies a pitch whose frame has passed before the next input', async () => {
    const r = await ready();
    quantum(r);
    quantum(r);
    r.port.send({ type: 'pitch', value: 0.5, atFrame: 0 });
    quantum(r);
    expect(r.fake.pitches).toEqual([{ value: 0.5, inFrames: 2 * Q }]);
  });

  it('applies queued changes in order and a newer message replaces pending ones at or after its frame', async () => {
    const r = await ready();
    quantum(r);
    const first = r.scope.currentFrame - Q;
    r.port.send({ type: 'pitch', value: 1.5, atFrame: first + 1000 });
    r.port.send({ type: 'pitch', value: 3, atFrame: first + 3000 });
    r.port.send({ type: 'pitch', value: 2, atFrame: first + 2000 });
    for (let q = 0; q < 40; q++) {
      quantum(r);
    }
    expect(r.fake.pitches.map((p) => p.value)).toEqual([1.5, 2]);
  });

  it('keeps pitch messages that arrive before ready', async () => {
    const r = rig();
    r.port.send({ type: 'pitch', value: 4, atFrame: 0 });
    await flush();
    quantum(r);
    expect(r.fake.pitches).toEqual([{ value: 4, inFrames: 0 }]);
  });

  it('ignores unknown messages', async () => {
    const r = await ready();
    r.port.send({ type: 'unknown' });
    r.port.send(null);
    expect(quantum(r).alive).toBe(true);
  });
});

describe('time-stretch processor: reset and dispose', () => {
  it('reset clears SoundTouch, refills the delay with silence and restarts the frame count', async () => {
    const fake = new FakeSoundTouch();
    const r = await ready({ fake, delayFrames: 256 });
    for (let q = 0; q < 4; q++) {
      quantum(r, block(1));
    }
    r.port.send({ type: 'reset' });
    expect(fake.clears).toBe(1);
    const after = [...left(quantum(r, block(1)).out), ...left(quantum(r, block(1)).out)];
    expect(after.every((v) => v === 0)).toBe(true);
    expect(left(quantum(r, block(1)).out)[0]).toBe(1);
    const first = r.scope.currentFrame - 3 * Q;
    r.port.send({ type: 'pitch', value: 2, atFrame: first + Q });
    quantum(r);
    quantum(r);
    expect(fake.pitches).toEqual([{ value: 2, inFrames: 3 * Q }]);
  });

  it('reset before ready is harmless', async () => {
    const r = rig();
    r.port.send({ type: 'reset' });
    await flush();
    expect(r.port.ofType('ready')).toHaveLength(1);
  });

  it('dispose destroys the instance, frees the buffers and ends processing', async () => {
    const r = await ready();
    r.port.send({ type: 'dispose' });
    expect(r.fake.destroyed).toEqual([4096]);
    expect(r.fake.freed).toHaveLength(2);
    expect(quantum(r).alive).toBe(false);
    r.port.send({ type: 'dispose' });
    expect(r.fake.destroyed).toHaveLength(1);
  });

  it('dispose before ready never creates an instance', async () => {
    const r = rig();
    r.port.send({ type: 'dispose' });
    await flush();
    expect(r.fake.created).toEqual([]);
    expect(r.port.ofType('ready')).toEqual([]);
    expect(quantum(r).alive).toBe(false);
  });
});

describe('instantiateSoundTouch', () => {
  it('instantiates the bytes without imports and calls _initialize once', async () => {
    const fake = new FakeSoundTouch();
    const spy = vi.spyOn(WebAssembly, 'instantiate').mockResolvedValue({
      instance: { exports: fake },
      module: {},
    } as never);
    const bytes = new ArrayBuffer(4);
    const exports = await instantiateSoundTouch(bytes);
    expect(spy).toHaveBeenCalledWith(bytes, {});
    expect(exports).toBe(fake);
    expect(fake.initialized).toBe(1);
  });

  it('accepts a module without _initialize', async () => {
    const fake = new FakeSoundTouch();
    Object.defineProperty(fake, '_initialize', { value: undefined });
    vi.spyOn(WebAssembly, 'instantiate').mockResolvedValue({
      instance: { exports: fake },
      module: {},
    } as never);
    await expect(instantiateSoundTouch(new ArrayBuffer(4))).resolves.toBe(fake);
  });
});

describe('processor module entry', () => {
  it('registers with the global scope when loaded in an AudioWorkletGlobalScope', async () => {
    const scope = new FakeWorkletScope();
    vi.stubGlobal('registerProcessor', scope.registerProcessor.bind(scope));
    vi.stubGlobal('AudioWorkletProcessor', scope.AudioWorkletProcessor);
    vi.resetModules();
    await import('../../src/stretch/processor');
    vi.unstubAllGlobals();
    expect(scope.registered.has(STRETCH_PROCESSOR_NAME)).toBe(true);
  });

  it('does nothing outside a worklet scope', async () => {
    vi.resetModules();
    await expect(import('../../src/stretch/processor')).resolves.toBeDefined();
  });
});
