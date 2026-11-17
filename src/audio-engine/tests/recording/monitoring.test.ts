import { describe, expect, it } from 'vitest';
import { Monitor } from '../../src/recording/monitoring';
import type { MockGainNode } from '../mocks/audio-context';
import { RecordingContext } from '../mocks/media';

/** Input monitoring (contract 2.5.0, FR-006): input -> Gain(volume) -> destination, bypassing master and take. */
function rig(): { ctx: RecordingContext; monitor: Monitor; input: MockGainNode } {
  const ctx = new RecordingContext();
  const monitor = new Monitor(ctx.asAudioContext());
  const input = ctx.createGain();
  return { ctx, monitor, input };
}

function gainOf(ctx: RecordingContext, input: MockGainNode): MockGainNode {
  const node = input.connections.find((n) => n.kind === 'gain') as MockGainNode | undefined;
  if (node === undefined) {
    throw new Error('not monitored');
  }
  expect(node.connections).toEqual([ctx.destination]);
  return node;
}

describe('Monitor', () => {
  it('is off by default: the input is connected through a silent gain straight to the destination', () => {
    const { ctx, monitor, input } = rig();
    monitor.attach(input as unknown as AudioNode);
    expect(gainOf(ctx, input).gain.value).toBe(0);
    expect(monitor.settings).toEqual({ enabled: false, volume: 0.7 });
  });

  it('ramps to the volume when enabled (time constant 3 ms, <= 10 ms) and back to 0 when disabled', () => {
    const { ctx, monitor, input } = rig();
    monitor.attach(input as unknown as AudioNode);
    ctx.currentTime = 2;
    monitor.set({ enabled: true, volume: 0.5 });
    const gain = gainOf(ctx, input).gain;
    expect(gain.calls.at(-1)).toEqual({ method: 'setTargetAtTime', args: [0.5, 2, 0.003] });
    monitor.set({ enabled: false, volume: 0.5 });
    expect(gain.value).toBe(0);
  });

  it('keeps the settings across inputs and applies them to a new input', () => {
    const { ctx, monitor, input } = rig();
    monitor.set({ enabled: true, volume: 0.4 });
    monitor.attach(input as unknown as AudioNode);
    expect(gainOf(ctx, input).gain.value).toBe(0.4);
    const next = ctx.createGain();
    monitor.attach(next as unknown as AudioNode);
    expect(gainOf(ctx, next).gain.value).toBe(0.4);
    monitor.attach(null);
    expect(gainOf(ctx, next).disconnected).toBe(true);
  });

  it('is forced off while suppressed (calibration) and restored afterwards', () => {
    const { ctx, monitor, input } = rig();
    monitor.attach(input as unknown as AudioNode);
    monitor.set({ enabled: true, volume: 0.9 });
    monitor.suppress(true);
    expect(gainOf(ctx, input).gain.value).toBe(0);
    monitor.set({ enabled: true, volume: 0.8 });
    expect(gainOf(ctx, input).gain.value).toBe(0);
    monitor.suppress(false);
    expect(gainOf(ctx, input).gain.value).toBe(0.8);
  });

  it('validates the volume (0..1, finite)', () => {
    const { monitor } = rig();
    expect(() => {
      monitor.set({ enabled: true, volume: 1.5 });
    }).toThrow('Invalid monitoring volume: 1.5 (0..1)');
    expect(() => {
      monitor.set({ enabled: true, volume: Number.NaN });
    }).toThrow('Invalid monitoring volume');
  });
});
