import { describe, expect, it } from 'vitest';
import { CAPTURE_BATCH_FRAMES, CAPTURE_PROCESSOR_NAME, registerCaptureProcessor, type CaptureProcessor, type CaptureScope } from '../../src/recording/capture-worklet';
import { FakeProcessorBase, type FakeProcessorPort } from '../mocks/worklet-scope';

/** The `stemstudio-capture` processor (research R-01). */
const QUANTUM = 128;

type ProcessorClass = new (options?: AudioWorkletNodeOptions) => CaptureProcessor;

class Scope implements CaptureScope {
  public readonly AudioWorkletProcessor = FakeProcessorBase as unknown as CaptureScope['AudioWorkletProcessor'];
  public readonly registered = new Map<string, ProcessorClass>();
  public currentFrame = 0;

  public registerProcessor(name: string, processor: ProcessorClass): void {
    this.registered.set(name, processor);
  }
}

interface Block {
  readonly type: string;
  readonly frame: number;
  readonly channels: Float32Array[];
}

interface Rig {
  readonly scope: Scope;
  readonly processor: CaptureProcessor;
  readonly port: FakeProcessorPort;
}

function make(options?: AudioWorkletNodeOptions): Rig {
  const scope = new Scope();
  registerCaptureProcessor(scope);
  const Processor = scope.registered.get(CAPTURE_PROCESSOR_NAME);
  if (Processor === undefined) {
    throw new Error('not registered');
  }
  const processor = new Processor(options);
  return { scope, processor, port: processor.port as unknown as FakeProcessorPort };
}

function withChannels(channels: 1 | 2): Rig {
  return make({ processorOptions: { channels } });
}

/** Runs `quanta` render quanta whose samples are `channel * 1000 + frame % 1000`. */
function render(rig: Rig, quanta: number, inputChannels = 2): boolean[] {
  const alive: boolean[] = [];
  for (let q = 0; q < quanta; q++) {
    const frame = rig.scope.currentFrame;
    const input = Array.from({ length: inputChannels }, (_, c) => Float32Array.from({ length: QUANTUM }, (_, i) => c * 1000 + ((frame + i) % 1000)));
    alive.push(rig.processor.process([input], [[new Float32Array(QUANTUM)]]));
    rig.scope.currentFrame += QUANTUM;
  }
  return alive;
}

function blocks(port: FakeProcessorPort): Block[] {
  return port.ofType('block') as unknown as Block[];
}

describe('capture processor', () => {
  it('posts nothing until started and keeps the node alive', () => {
    const rig = withChannels(1);
    expect(render(rig, 32).every(Boolean)).toBe(true);
    expect(blocks(rig.port)).toEqual([]);
  });

  it('posts 2,048-frame batches stamped with the currentFrame of their first frame', () => {
    const rig = withChannels(1);
    rig.scope.currentFrame = 128 * 10;
    rig.port.send({ type: 'start' });
    render(rig, 33);
    const posted = blocks(rig.port);
    expect(CAPTURE_BATCH_FRAMES).toBe(2048);
    expect(posted.map((b) => b.frame)).toEqual([1280, 1280 + 2048]);
    expect(posted.map((b) => b.channels.map((c) => c.length))).toEqual([[2048], [2048]]);
    expect(posted[1]?.channels[0]?.[0]).toBe((1280 + 2048) % 1000);
  });

  it('copies channel 0 for one channel and channels 0 and 1 for two', () => {
    const mono = withChannels(1);
    const stereo = withChannels(2);
    [mono, stereo].forEach((rig) => {
      rig.port.send({ type: 'start' });
      render(rig, 16);
    });
    expect(blocks(mono.port)[0]?.channels).toHaveLength(1);
    const [left, right] = blocks(stereo.port)[0]?.channels ?? [];
    expect([left?.[5], right?.[5]]).toEqual([5, 1005]);
  });

  it('writes zeros for missing input channels (no input connected yet)', () => {
    const rig = withChannels(2);
    rig.port.send({ type: 'start' });
    render(rig, 16, 0);
    const [left, right] = blocks(rig.port)[0]?.channels ?? [];
    expect([left?.every((x) => x === 0), right?.every((x) => x === 0)]).toEqual([true, true]);
  });

  it('stop flushes the partial batch and ends posting; a restart stamps afresh', () => {
    const rig = withChannels(1);
    rig.port.send({ type: 'start' });
    render(rig, 3);
    rig.port.send({ type: 'stop' });
    render(rig, 20);
    expect(blocks(rig.port).map((b) => [b.frame, b.channels[0]?.length])).toEqual([[0, 384]]);
    rig.port.send({ type: 'stop' });
    rig.port.send({ type: 'start' });
    render(rig, 16);
    expect(blocks(rig.port).at(-1)?.frame).toBe(23 * 128);
  });

  it('ignores unknown messages and defaults to one channel without options', () => {
    const rig = make();
    rig.port.send({ type: 'start' });
    rig.port.send({ type: 'nonsense' });
    rig.port.send(null);
    render(rig, 16);
    expect(blocks(rig.port)[0]?.channels).toHaveLength(1);
  });
});
