import { describe, expect, it } from 'vitest';
import { MockAudioBuffer, MockAudioContext } from './audio-context';
import { FakeDotNetRef } from './dotnet-ref';
import { FetchStub } from './fetch';

describe('MockAudioContext', () => {
  it('records sources, start/stop calls and loop settings', () => {
    const ctx = new MockAudioContext();
    const source = ctx.createBufferSource();
    source.loop = true;
    source.loopStart = 1;
    source.loopEnd = 2;
    source.start(0.1, 3);
    source.stop();
    expect(ctx.sources).toHaveLength(1);
    expect(source.startCalls).toEqual([{ when: 0.1, offset: 3 }]);
    expect(source.stopCalls).toEqual([undefined]);
    expect(ctx.startedSources()).toEqual([source]);
    expect(() => {
      source.start(1);
    }).toThrow('start() called twice');
  });

  it('records the node graph and disconnects', () => {
    const ctx = new MockAudioContext();
    const gain = ctx.createGain();
    const panner = ctx.createStereoPanner();
    gain.connect(panner).connect(ctx.destination);
    expect(gain.connections).toEqual([panner]);
    expect(panner.connections).toEqual([ctx.destination]);
    gain.disconnect();
    expect(gain.disconnected).toBe(true);
    expect(gain.connections).toEqual([]);
    expect(ctx.gains).toEqual([gain]);
    expect(ctx.panners).toEqual([panner]);
  });

  it('records AudioParam automation', () => {
    const ctx = new MockAudioContext();
    const { gain } = ctx.createGain();
    gain.cancelScheduledValues(0).setValueAtTime(0.5, 0).linearRampToValueAtTime(0.2, 0.01).setTargetAtTime(0.7, 0, 0.003);
    expect(gain.value).toBe(0.7);
    expect(gain.calls.map((c) => c.method)).toEqual([
      'cancelScheduledValues',
      'setValueAtTime',
      'linearRampToValueAtTime',
      'setTargetAtTime',
    ]);
  });

  it('serves analyser samples and fires onended', () => {
    const ctx = new MockAudioContext();
    const analyser = ctx.createAnalyser();
    analyser.samples = [0.5, -1];
    const target = new Float32Array(4).fill(9);
    analyser.getFloatTimeDomainData(target);
    expect(Array.from(target)).toEqual([0.5, -1, 0, 0]);
    const source = ctx.createBufferSource();
    let ended = 0;
    source.end();
    source.onended = () => {
      ended += 1;
    };
    source.end();
    expect(ended).toBe(1);
    expect(ctx.analysers).toEqual([analyser]);
  });

  it('decodes to buffers of a scripted duration and can fail', async () => {
    const ctx = new MockAudioContext();
    ctx.decodeDurations.set(8, 2);
    const buffer = await ctx.decodeAudioData(new ArrayBuffer(8));
    expect(buffer.duration).toBe(2);
    expect((await ctx.decodeAudioData(new ArrayBuffer(1))).duration).toBe(10);
    ctx.decodeError = new Error('bad data');
    await expect(ctx.decodeAudioData(new ArrayBuffer(1))).rejects.toThrow('bad data');
    expect(ctx.decoded).toHaveLength(3);
  });

  it('creates buffers with channel data, resumes and closes', async () => {
    const ctx = new MockAudioContext(48000);
    const buffer = ctx.createBuffer(1, 480, 48000);
    expect(buffer).toBeInstanceOf(MockAudioBuffer);
    expect(buffer.getChannelData(0)).toHaveLength(480);
    expect(() => buffer.getChannelData(1)).toThrow('No channel 1');
    await ctx.resume();
    expect(ctx.state).toBe('running');
    expect(ctx.resumeCount).toBe(1);
    await ctx.close();
    expect(ctx.state).toBe('closed');
    expect(ctx.asAudioContext()).toBe(ctx);
  });
});

describe('FakeDotNetRef', () => {
  it('records calls per method and clears', async () => {
    const ref = new FakeDotNetRef();
    await ref.invokeMethodAsync('OnPositionChanged', 1.5);
    await ref.invokeMethodAsync('OnPlaybackStateChanged', 'playing');
    expect(ref.argsOf('OnPositionChanged')).toEqual([1.5]);
    expect(ref.argsOf('OnPlaybackStateChanged')).toEqual(['playing']);
    ref.clear();
    expect(ref.calls).toEqual([]);
  });
});

describe('FetchStub', () => {
  it('replies per URL, defaults to 404 and can fail', async () => {
    const stub = new FetchStub().reply('/a', { bytes: 4 }).reply('/b', { status: 500 }).reply('/c', { error: new Error('offline') });
    const ok = await stub.fetch('/a');
    expect(ok.ok).toBe(true);
    expect((await ok.arrayBuffer()).byteLength).toBe(4);
    expect((await stub.fetch(new URL('http://x/b'))).ok).toBe(false);
    expect((await stub.fetch('/b')).status).toBe(500);
    expect((await stub.fetch(new Request('http://x/missing'))).status).toBe(404);
    await expect(stub.fetch('/c')).rejects.toThrow('offline');
    expect(stub.requested).toEqual(['/a', 'http://x/b', '/b', 'http://x/missing', '/c']);
  });
});
