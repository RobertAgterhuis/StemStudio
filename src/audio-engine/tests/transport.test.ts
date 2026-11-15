import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DotNetNotifier } from '../src/callbacks';
import { loadStems, trackDuration } from '../src/stem-loader';
import { Transport, type TransportListener } from '../src/transport';
import type { ChannelId, LoopRegion, StemName } from '../src/types';
import { MockAudioContext, type MockAudioBufferSourceNode, type MockGainNode, type StartCall } from './mocks/audio-context';
import { FakeDotNetRef } from './mocks/dotnet-ref';
import { FetchStub } from './mocks/fetch';

const STEMS: StemName[] = ['vocals', 'drums', 'bass', 'other'];
const NO_LOOP: LoopRegion = { start: 0, end: 0, enabled: false };

interface Rig {
  readonly ctx: MockAudioContext;
  readonly transport: Transport;
  readonly ref: FakeDotNetRef;
  readonly outputs: Map<ChannelId, MockGainNode>;
}

async function makeRig(durations: readonly number[] = [10, 10, 10, 12]): Promise<Rig> {
  const ctx = new MockAudioContext();
  const outputs = new Map<ChannelId, MockGainNode>();
  const ref = new FakeDotNetRef();
  const transport = new Transport({
    context: ctx.asAudioContext(),
    startDelaySeconds: 0.1,
    output: (name) => {
      const node = ctx.createGain();
      outputs.set(name, node);
      return node as unknown as AudioNode;
    },
  });
  transport.addListener(new DotNetNotifier(ref, () => transport.position()));
  const buffers = new Map<StemName, AudioBuffer>();
  STEMS.forEach((name, i) => {
    const seconds = durations[i] ?? 10;
    buffers.set(name, ctx.createBuffer(2, seconds * ctx.sampleRate, ctx.sampleRate) as unknown as AudioBuffer);
  });
  transport.load(buffers);
  return Promise.resolve({ ctx, transport, ref, outputs });
}

/** Sources created by the transport since `from` (index into ctx.sources). */
function startsSince(ctx: MockAudioContext, from: number): MockAudioBufferSourceNode[] {
  return ctx.sources.slice(from);
}

function firstStart(group: readonly MockAudioBufferSourceNode[]): StartCall {
  const call = group[0]?.startCalls[0];
  if (call === undefined) {
    throw new Error('no start call');
  }
  return call;
}

function expectSampleLocked(group: readonly MockAudioBufferSourceNode[], stems = STEMS.length): void {
  expect(group).toHaveLength(stems);
  const first = group[0]?.startCalls[0];
  expect(first).toBeDefined();
  for (const source of group) {
    expect(source.startCalls).toEqual([first]);
  }
}

describe('stem-loader', () => {
  it('fetches and decodes every stem in parallel and reports the longest duration', async () => {
    const ctx = new MockAudioContext();
    ctx.decodeDurations.set(100, 30);
    ctx.decodeDurations.set(200, 42.5);
    const stub = new FetchStub().reply('/s/vocals', { bytes: 100 }).reply('/s/drums', { bytes: 200 });
    const buffers = await loadStems(ctx.asAudioContext(), stub.fetch, [
      { name: 'vocals', url: '/s/vocals' },
      { name: 'drums', url: '/s/drums' },
    ]);
    expect(stub.requested).toEqual(['/s/vocals', '/s/drums']);
    expect([...buffers.keys()]).toEqual(['vocals', 'drums']);
    expect(trackDuration(buffers)).toBe(42.5);
    expect(trackDuration(new Map())).toBe(0);
  });

  it('names the stem and the HTTP status when a download fails', async () => {
    const ctx = new MockAudioContext();
    const stub = new FetchStub().reply('/ok', {}).reply('/bad', { status: 404 });
    await expect(
      loadStems(ctx.asAudioContext(), stub.fetch, [
        { name: 'vocals', url: '/ok' },
        { name: 'bass', url: '/bad' },
      ]),
    ).rejects.toThrow('Failed to load stem "bass": HTTP 404');
  });

  it('names the stem on network and decode errors', async () => {
    const ctx = new MockAudioContext();
    const stub = new FetchStub().reply('/net', { error: new Error('offline') }).reply('/ok', {});
    await expect(loadStems(ctx.asAudioContext(), stub.fetch, [{ name: 'piano', url: '/net' }])).rejects.toThrow(
      'Failed to load stem "piano": offline',
    );
    ctx.decodeError = new Error('EncodingError');
    await expect(loadStems(ctx.asAudioContext(), stub.fetch, [{ name: 'guitar', url: '/ok' }])).rejects.toThrow(
      'Failed to decode stem "guitar": EncodingError',
    );
  });

  it('reports non-Error rejections as text', async () => {
    const ctx = new MockAudioContext();
    const failing = vi.fn<typeof fetch>().mockRejectedValue('socket closed');
    await expect(loadStems(ctx.asAudioContext(), failing, [{ name: 'drums', url: '/x' }])).rejects.toThrow(
      'Failed to load stem "drums": socket closed',
    );
  });

  it('rejects duplicate stem names', async () => {
    const ctx = new MockAudioContext();
    const stub = new FetchStub().reply('/a', {});
    await expect(
      loadStems(ctx.asAudioContext(), stub.fetch, [
        { name: 'bass', url: '/a' },
        { name: 'bass', url: '/a' },
      ]),
    ).rejects.toThrow('Duplicate stem "bass"');
  });
});

describe('transport', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('reports the duration of the longest stem and starts stopped at 0', async () => {
    const { transport } = await makeRig();
    expect(transport.duration).toBe(12);
    expect(transport.state).toBe('stopped');
    expect(transport.position()).toBe(0);
    expect(transport.anchor()).toBeNull();
  });

  it('play resumes the context and starts every stem with the same when and offset', async () => {
    const { ctx, transport, outputs } = await makeRig();
    ctx.currentTime = 3;
    await transport.play();
    expect(ctx.resumeCount).toBe(1);
    const group = ctx.startedSources();
    expectSampleLocked(group);
    expect(group[0]?.startCalls[0]).toEqual({ when: 3.1, offset: 0 });
    expect(transport.anchor()).toEqual({ when: 3.1, offset: 0 });
    group.forEach((source, i) => {
      expect(source.buffer).not.toBeNull();
      expect(source.connections).toEqual([outputs.get(STEMS[i] ?? 'vocals')]);
    });
    expect(transport.state).toBe('playing');
  });

  it('a second play while playing does not start new sources', async () => {
    const { ctx, transport } = await makeRig();
    await transport.play();
    await transport.play();
    expect(ctx.sources).toHaveLength(STEMS.length);
  });

  it('position is offset + (currentTime - when), never before the offset', async () => {
    const { ctx, transport } = await makeRig();
    ctx.currentTime = 1;
    await transport.play();
    expect(transport.position()).toBe(0);
    ctx.currentTime = 3.6;
    expect(transport.position()).toBeCloseTo(2.5, 9);
    ctx.currentTime = 100;
    expect(transport.position()).toBe(12);
  });

  it('pause stores the position, stops all sources and resumes from there with fresh sources', async () => {
    const { ctx, transport } = await makeRig();
    await transport.play();
    ctx.currentTime = 2.1;
    transport.pause();
    expect(transport.state).toBe('paused');
    expect(transport.position()).toBeCloseTo(2, 9);
    for (const source of ctx.sources) {
      expect(source.stopCalls).toHaveLength(1);
      expect(source.disconnected).toBe(true);
    }
    transport.pause();
    expect(transport.state).toBe('paused');
    ctx.currentTime = 5;
    await transport.play();
    const resumed = startsSince(ctx, STEMS.length);
    expectSampleLocked(resumed);
    expect(firstStart(resumed).when).toBeCloseTo(5.1, 9);
    expect(firstStart(resumed).offset).toBeCloseTo(2, 9);
  });

  it('stop resets the position to 0 and play starts from 0', async () => {
    const { ctx, transport } = await makeRig();
    await transport.play();
    ctx.currentTime = 4;
    transport.stop();
    expect(transport.state).toBe('stopped');
    expect(transport.position()).toBe(0);
    await transport.play();
    expect(startsSince(ctx, STEMS.length)[0]?.startCalls[0]?.offset).toBe(0);
  });

  it('seek clamps to 0..duration, restarts all stems while playing and only moves while paused', async () => {
    const { ctx, transport } = await makeRig();
    transport.seek(-3);
    expect(transport.position()).toBe(0);
    transport.seek(99);
    expect(transport.position()).toBe(12);
    transport.seek(4);
    expect(transport.position()).toBe(4);
    expect(ctx.sources).toHaveLength(0);
    await transport.play();
    ctx.currentTime = 1;
    transport.seek(7);
    const group = startsSince(ctx, STEMS.length);
    expectSampleLocked(group);
    expect(group[0]?.startCalls[0]).toEqual({ when: 1.1, offset: 7 });
    expect(transport.state).toBe('playing');
  });

  it('end of the longest stem stops the transport at position 0', async () => {
    const { ctx, transport, ref } = await makeRig();
    await transport.play();
    const longest = ctx.sources[3];
    ctx.sources[0]?.end();
    expect(transport.state).toBe('playing');
    longest?.end();
    expect(transport.state).toBe('stopped');
    expect(transport.position()).toBe(0);
    expect(ref.argsOf('OnPlaybackStateChanged')).toEqual(['playing', 'stopped']);
  });

  it('ignores ended events of sources that were already replaced', async () => {
    const { ctx, transport } = await makeRig();
    await transport.play();
    const old = ctx.sources[3];
    const handler = old?.onended;
    transport.seek(2);
    handler?.();
    expect(transport.state).toBe('playing');
  });

  it('load replaces the track and stops playback', async () => {
    const { ctx, transport } = await makeRig();
    await transport.play();
    transport.load(new Map([['bass', ctx.createBuffer(2, 44100, 44100) as unknown as AudioBuffer]]));
    expect(transport.state).toBe('stopped');
    expect(transport.duration).toBe(1);
    await transport.play();
    expect(startsSince(ctx, STEMS.length)).toHaveLength(1);
  });

  it('play without a loaded track does nothing', async () => {
    const ctx = new MockAudioContext();
    const transport = new Transport({ context: ctx.asAudioContext(), startDelaySeconds: 0.1, output: () => ctx.destination as unknown as AudioNode });
    await transport.play();
    expect(transport.state).toBe('stopped');
    expect(ctx.sources).toHaveLength(0);
  });

  it('reports state changes and positions to .NET', async () => {
    const { ctx, transport, ref } = await makeRig();
    await transport.play();
    ctx.currentTime = 1.1;
    transport.seek(5);
    transport.pause();
    transport.stop();
    expect(ref.argsOf('OnPlaybackStateChanged')).toEqual(['playing', 'paused', 'stopped']);
    expect(ref.argsOf('OnPositionChanged')).toEqual([5, 5, 0]);
  });

  it('reports the position at most 10 times per second while playing and not when stopped', async () => {
    const { ctx, transport, ref } = await makeRig();
    await transport.play();
    for (let ms = 0; ms < 1000; ms += 10) {
      ctx.currentTime = 0.1 + ms / 1000;
      vi.advanceTimersByTime(10);
    }
    const reports = ref.argsOf('OnPositionChanged');
    expect(reports.length).toBeLessThanOrEqual(10);
    expect(reports.length).toBeGreaterThanOrEqual(9);
    expect(reports.at(-1)).toBeCloseTo(0.99, 2);
    transport.stop();
    ref.clear();
    vi.advanceTimersByTime(1000);
    expect(ref.calls).toEqual([]);
  });

  it('swallows rejected .NET callbacks (circuit gone)', async () => {
    const { transport, ref } = await makeRig();
    vi.spyOn(ref, 'invokeMethodAsync').mockRejectedValue(new Error('circuit disconnected'));
    await transport.play();
    transport.stop();
    await vi.runOnlyPendingTimersAsync();
    expect(transport.state).toBe('stopped');
  });

  it('notifies listeners about repositioning after seek, pause, stop', async () => {
    const { ctx, transport } = await makeRig();
    const events: string[] = [];
    const listener: TransportListener = {
      onStateChanged: (state) => events.push(`state:${state}`),
      onReposition: (position) => events.push(`pos:${String(position)}`),
    };
    transport.addListener(listener);
    await transport.play();
    ctx.currentTime = 0.6;
    transport.seek(3);
    transport.stop();
    expect(events).toEqual(['state:playing', 'pos:3', 'state:stopped', 'pos:0']);
  });

  describe('loop', () => {
    it('applies loop, loopStart and loopEnd to every source it starts', async () => {
      const { ctx, transport } = await makeRig();
      transport.setLoop({ start: 2, end: 4, enabled: true });
      await transport.play();
      for (const source of ctx.sources) {
        expect([source.loop, source.loopStart, source.loopEnd]).toEqual([true, 2, 4]);
      }
      expect(ctx.sources[0]?.startCalls[0]?.offset).toBe(2);
      expect(transport.loop).toEqual({ start: 2, end: 4, enabled: true });
    });

    it('wraps the position inside the region', async () => {
      const { ctx, transport } = await makeRig();
      transport.setLoop({ start: 2, end: 4, enabled: true });
      await transport.play();
      ctx.currentTime = 0.1 + 2.5;
      expect(transport.position()).toBeCloseTo(2.5, 9);
      ctx.currentTime = 0.1 + 7.25;
      expect(transport.position()).toBeCloseTo(3.25, 9);
    });

    it('G4: 50 loop wraps and 20 seeks keep every transport event sample-locked', async () => {
      const { ctx, transport } = await makeRig();
      const loop = { start: 1.5, end: 3.75, enabled: true };
      const length = loop.end - loop.start;
      transport.setLoop(loop);
      await transport.play();
      let groupStart = 0;
      const checkGroup = (): void => {
        expectSampleLocked(startsSince(ctx, groupStart));
        groupStart = ctx.sources.length;
      };
      checkGroup();
      let wraps = 0;
      let seeks = 0;
      const advanceThreeWraps = (): void => {
        const { when, offset } = transport.anchor() ?? { when: 0, offset: 0 };
        ctx.currentTime = when + (loop.end - offset) + length * 2.5;
        const expected = loop.start + ((ctx.currentTime - when + offset - loop.start) % length);
        expect(transport.position()).toBeCloseTo(expected, 9);
        expect(transport.position()).toBeGreaterThanOrEqual(loop.start);
        expect(transport.position()).toBeLessThan(loop.end);
      };
      while (wraps < 50) {
        advanceThreeWraps();
        wraps += 3;
        if (seeks < 20) {
          transport.seek(loop.start + (seeks % 7) * 0.3);
          seeks += 1;
          checkGroup();
        }
      }
      for (; seeks < 20; seeks += 1) {
        transport.seek(loop.start + 0.1);
        checkGroup();
      }
      expect(wraps).toBeGreaterThanOrEqual(50);
      expect(seeks).toBe(20);
      expect(ctx.sources).toHaveLength(STEMS.length * 21);
      for (const source of ctx.sources) {
        expect([source.loop, source.loopStart, source.loopEnd]).toEqual([true, loop.start, loop.end]);
      }
    });

    it('enabling a loop while inside the region keeps the sources and the position', async () => {
      const { ctx, transport } = await makeRig();
      await transport.play();
      ctx.currentTime = 2.6;
      transport.setLoop({ start: 2, end: 4, enabled: true });
      expect(ctx.sources).toHaveLength(STEMS.length);
      expect(ctx.sources.every((s) => s.loop && s.loopStart === 2 && s.loopEnd === 4)).toBe(true);
      expect(transport.position()).toBeCloseTo(2.5, 9);
      ctx.currentTime = 2.6 + 1.5 + 2;
      expect(transport.position()).toBeCloseTo(2, 9);
    });

    it('enabling a loop while outside the region restarts all stems at loop start', async () => {
      const { ctx, transport } = await makeRig();
      await transport.play();
      ctx.currentTime = 6;
      transport.setLoop({ start: 2, end: 4, enabled: true });
      const group = startsSince(ctx, STEMS.length);
      expectSampleLocked(group);
      expect(group[0]?.startCalls[0]).toEqual({ when: 6.1, offset: 2 });
    });

    it('disabling the loop clears loop on running sources and continues linearly', async () => {
      const { ctx, transport } = await makeRig();
      transport.setLoop({ start: 2, end: 4, enabled: true });
      await transport.play();
      ctx.currentTime = 0.1 + 3.5;
      transport.setLoop({ start: 2, end: 4, enabled: false });
      expect(ctx.sources.every((s) => !s.loop)).toBe(true);
      expect(transport.position()).toBeCloseTo(3.5, 9);
      ctx.currentTime += 1;
      expect(transport.position()).toBeCloseTo(4.5, 9);
    });

    it('a loop change during the start delay does not advance the position early', async () => {
      const { ctx, transport } = await makeRig();
      await transport.play();
      ctx.currentTime = 0.05;
      transport.setLoop({ start: 0, end: 4, enabled: true });
      expect(transport.anchor()).toEqual({ when: 0.1, offset: 0 });
      expect(transport.position()).toBe(0);
    });

    it('seek outside the active loop region restarts at loop start; paused play does the same', async () => {
      const { ctx, transport } = await makeRig();
      transport.setLoop({ start: 2, end: 4, enabled: true });
      transport.seek(8);
      expect(transport.position()).toBe(2);
      await transport.play();
      ctx.currentTime = 1;
      transport.seek(9);
      expect(startsSince(ctx, STEMS.length)[0]?.startCalls[0]?.offset).toBe(2);
      transport.setLoop(NO_LOOP);
      transport.seek(9);
      expect(transport.position()).toBe(9);
    });

    it('stop while looping resets to 0, not to the loop start', async () => {
      const { ctx, transport } = await makeRig();
      transport.setLoop({ start: 2, end: 4, enabled: true });
      await transport.play();
      ctx.currentTime = 1;
      transport.stop();
      expect(transport.position()).toBe(0);
    });
  });
});
