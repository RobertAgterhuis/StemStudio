import { describe, expect, it } from 'vitest';
import { STRETCH_DELAY_FRAMES } from '../../src/stretch/constants';
import { FakeStretchStage } from '../../src/stretch/fake-stage';
import { DEFAULT_PROCESSOR_URL, DEFAULT_WASM_URL, createWorkletStageFactory, type FailureAware } from '../../src/stretch/worklet-stage';
import type { TimeStretchStage } from '../../src/types';
import { MockAudioContext } from '../mocks/audio-context';
import { FetchStub } from '../mocks/fetch';
import { WorkletContext, type MockAudioWorkletNode } from '../mocks/worklet-node';

const WASM_BYTES = 30;

function rig(options: { status?: number; processorUrl?: string; wasmUrl?: string } = {}) {
  const ctx = new WorkletContext();
  const fetch = new FetchStub();
  const { status, ...urls } = { status: 200, processorUrl: DEFAULT_PROCESSOR_URL, wasmUrl: DEFAULT_WASM_URL, ...options };
  fetch.reply(urls.wasmUrl, { status, bytes: WASM_BYTES });
  const factory = createWorkletStageFactory({
    fetch: fetch.fetch,
    createNode: (context, name, nodeOptions) => (context as unknown as WorkletContext).createWorkletNode(name, nodeOptions) as unknown as AudioWorkletNode,
    ...urls,
  });
  return { ctx, fetch, factory };
}

function nodeOf(r: ReturnType<typeof rig>, index = 0): MockAudioWorkletNode {
  const node = r.ctx.workletNodes[index];
  if (node === undefined) {
    throw new Error('no worklet node');
  }
  return node;
}

const flush = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));

async function readyStage(r: ReturnType<typeof rig>): Promise<TimeStretchStage & FailureAware> {
  const pending = r.factory(r.ctx.asAudioContext());
  await flush();
  r.ctx.workletNodes.at(-1)?.port.reply({ type: 'ready', latencyFrames: STRETCH_DELAY_FRAMES });
  return (await pending) as TimeStretchStage & FailureAware;
}

describe('worklet stage loading (FR-007, FR-009, SC-007)', () => {
  it('requests nothing until the factory is called', () => {
    const r = rig();
    expect(r.ctx.audioWorklet.modules).toEqual([]);
    expect(r.fetch.requested).toEqual([]);
  });

  it('adds the processor module, fetches the WASM bytes and passes them in processorOptions', async () => {
    const r = rig();
    const stage = await readyStage(r);
    expect(r.ctx.audioWorklet.modules).toEqual(['./js/time-stretch-processor.js']);
    expect(r.fetch.requested).toEqual(['./js/soundtouch/soundtouch.wasm']);
    const [node] = r.ctx.workletNodes;
    expect(node?.name).toBe('stemstudio-time-stretch');
    expect(node?.options).toMatchObject({
      numberOfInputs: 1,
      numberOfOutputs: 1,
      outputChannelCount: [2],
      channelCount: 2,
      channelCountMode: 'explicit',
      processorOptions: { delayFrames: STRETCH_DELAY_FRAMES },
    });
    expect((node?.options.processorOptions as { wasmBytes: ArrayBuffer }).wasmBytes.byteLength).toBe(WASM_BYTES);
    expect(stage.latencyFrames).toBe(STRETCH_DELAY_FRAMES);
    expect(stage.input).toBe(node);
    expect(stage.output).toBe(node);
  });

  it('uses the configured URLs and loads the module and the bytes once per engine', async () => {
    const r = rig({ processorUrl: '/p.js', wasmUrl: '/s.wasm' });
    await readyStage(r);
    await readyStage(r);
    expect(r.ctx.audioWorklet.modules).toEqual(['/p.js']);
    expect(r.fetch.requested).toEqual(['/s.wasm']);
    expect(r.ctx.workletNodes).toHaveLength(2);
  });

  it('rejects without an AudioWorklet', async () => {
    const r = rig();
    await expect(r.factory(new MockAudioContext().asAudioContext())).rejects.toThrow('Time stretch unavailable: AudioWorklet is not supported');
  });

  it('rejects when the processor module fails to load and retries it on the next call', async () => {
    const r = rig();
    r.ctx.audioWorklet.error = new Error('Unable to load a worklet');
    await expect(r.factory(r.ctx.asAudioContext())).rejects.toThrow('Time stretch unavailable: Unable to load a worklet');
    r.ctx.audioWorklet.error = null;
    await readyStage(r);
    expect(r.ctx.audioWorklet.modules).toHaveLength(2);
  });

  it('rejects when the WASM file is missing (HTTP 404) and refetches on the next call', async () => {
    const r = rig({ status: 404 });
    await expect(r.factory(r.ctx.asAudioContext())).rejects.toThrow(
      'Time stretch unavailable: ./js/soundtouch/soundtouch.wasm returned HTTP 404',
    );
    r.fetch.reply(DEFAULT_WASM_URL, { bytes: WASM_BYTES });
    await readyStage(r);
    expect(r.fetch.requested).toHaveLength(2);
  });

  it('rejects when the processor reports an instantiate error', async () => {
    const r = rig();
    const pending = r.factory(r.ctx.asAudioContext());
    await flush();
    nodeOf(r).port.reply({ type: 'stats', underruns: 0 });
    nodeOf(r).port.reply({ type: 'error', message: 'bad magic' });
    await expect(pending).rejects.toThrow('Time stretch unavailable: bad magic');
    expect(r.ctx.workletNodes[0]?.disconnected).toBe(true);
  });

  it('rejects when the processor throws before ready', async () => {
    const r = rig();
    const pending = r.factory(r.ctx.asAudioContext());
    await flush();
    r.ctx.workletNodes[0]?.onprocessorerror?.(new Event('processorerror'));
    await expect(pending).rejects.toThrow('Time stretch unavailable: processor error');
  });

  it('rejects with a readable reason for a non-error failure', async () => {
    const r = rig();
    const failing = createWorkletStageFactory({ fetch: () => Promise.reject(new Error('offline')), createNode: () => { throw new Error('unused'); } });
    await expect(failing(r.ctx.asAudioContext())).rejects.toThrow('Time stretch unavailable: offline');
    // eslint-disable-next-line @typescript-eslint/prefer-promise-reject-errors -- a non-Error rejection is the case under test
    const odd = createWorkletStageFactory({ fetch: () => Promise.reject('gone'), createNode: () => { throw new Error('unused'); } });
    await expect(odd(r.ctx.asAudioContext())).rejects.toThrow('Time stretch unavailable: gone');
  });
});

describe('worklet stage messages', () => {
  it('sends pitch, reset and dispose over the port and reads underruns from stats', async () => {
    const r = rig();
    const stage = await readyStage(r);
    const node = nodeOf(r);
    stage.setPitch(2, 88200);
    stage.reset();
    expect(stage.underruns()).toBe(0);
    node.port.reply({ type: 'stats', underruns: 384 });
    node.port.reply({ type: 'unknown' });
    node.port.reply(null);
    expect(stage.underruns()).toBe(384);
    stage.dispose();
    expect(node.port.posted).toEqual([
      { type: 'pitch', value: 2, atFrame: 88200 },
      { type: 'reset' },
      { type: 'dispose' },
    ]);
    expect(node.disconnected).toBe(true);
  });

  it('reports a processor error after ready to the failure handler', async () => {
    const r = rig();
    const stage = await readyStage(r);
    const reasons: string[] = [];
    stage.onfailure = (reason) => {
      reasons.push(reason);
    };
    const node = nodeOf(r);
    node.port.reply({ type: 'error', message: 'trap' });
    node.onprocessorerror?.(new Event('processorerror'));
    expect(reasons).toEqual(['trap', 'processor error']);
  });

  it('ignores errors after ready without a failure handler', async () => {
    const r = rig();
    await readyStage(r);
    expect(() => r.ctx.workletNodes[0]?.port.reply({ type: 'error', message: 'trap' })).not.toThrow();
  });

  it('uses the default node factory (global AudioWorkletNode)', async () => {
    const ctx = new WorkletContext();
    const fetch = new FetchStub();
    fetch.reply(DEFAULT_WASM_URL, { bytes: WASM_BYTES });
    const created: unknown[] = [];
    class GlobalNode {
      public readonly port = { postMessage: (): void => undefined, onmessage: null };
      public constructor(...args: unknown[]) {
        created.push(args);
      }
    }
    const original = (globalThis as { AudioWorkletNode?: unknown }).AudioWorkletNode;
    (globalThis as { AudioWorkletNode?: unknown }).AudioWorkletNode = GlobalNode;
    void createWorkletStageFactory({ fetch: fetch.fetch })(ctx.asAudioContext());
    await flush();
    (globalThis as { AudioWorkletNode?: unknown }).AudioWorkletNode = original;
    expect(created).toHaveLength(1);
    expect(DEFAULT_PROCESSOR_URL).toBe('./js/time-stretch-processor.js');
  });
});

describe('fake stage (Vitest seam)', () => {
  it('records pitch and reset calls, reports underruns and can fail', () => {
    const ctx = new MockAudioContext();
    const stage = new FakeStretchStage(ctx.asAudioContext());
    stage.setPitch(0.5, 100);
    stage.reset();
    stage.underrunCount = 7;
    expect(stage.pitches).toEqual([{ factor: 0.5, atFrame: 100 }]);
    expect(stage.resets).toBe(1);
    expect(stage.underruns()).toBe(7);
    expect(stage.input).toBe(stage.output);
    expect(stage.latencyFrames).toBe(STRETCH_DELAY_FRAMES);
    const reasons: string[] = [];
    stage.fail('ignored without handler');
    stage.onfailure = (reason) => {
      reasons.push(reason);
    };
    stage.fail('trap');
    stage.dispose();
    expect(reasons).toEqual(['trap']);
    expect(stage.disposed).toBe(true);
  });
});
