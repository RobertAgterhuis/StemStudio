import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Engine } from '../src/engine';
import { ENGINE_CONTRACT_VERSION, createEngine, type AudioEngine, type MetronomeSettings } from '../src/index';
import { MockAudioContext } from './mocks/audio-context';
import { FakeDotNetRef } from './mocks/dotnet-ref';
import { FetchStub } from './mocks/fetch';

const METRONOME: MetronomeSettings = { enabled: true, volume: 0.5, bpmOverride: null, offsetMs: 0, beatsPerBar: 4 };

interface Rig {
  readonly ctx: MockAudioContext;
  readonly ref: FakeDotNetRef;
  readonly stub: FetchStub;
  readonly engine: AudioEngine;
}

function makeRig(): Rig {
  const ctx = new MockAudioContext();
  ctx.decodeDurations.set(100, 20);
  ctx.decodeDurations.set(200, 30);
  const ref = new FakeDotNetRef();
  const stub = new FetchStub().reply('/vocals', { bytes: 100 }).reply('/drums', { bytes: 200 });
  const engine = createEngine(ref, { audioContextFactory: () => ctx.asAudioContext(), fetch: stub.fetch });
  return { ctx, ref, stub, engine };
}

async function loaded(): Promise<Rig> {
  const rig = makeRig();
  await rig.engine.loadTrack({
    stems: [
      { name: 'vocals', url: '/vocals' },
      { name: 'drums', url: '/drums' },
    ],
    beatGrid: null,
  });
  return rig;
}

describe('module exports', () => {
  it('exposes the contract version', () => {
    expect(ENGINE_CONTRACT_VERSION).toBe('1.0.0');
  });
});

describe('createEngine', () => {
  const frames: FrameRequestCallback[] = [];

  beforeEach(() => {
    frames.length = 0;
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => frames.push(cb));
    vi.stubGlobal('cancelAnimationFrame', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('loadTrack fetches and decodes every stem and returns the duration', async () => {
    const { engine, stub, ctx } = await loaded();
    expect(stub.requested).toEqual(['/vocals', '/drums']);
    expect(ctx.decoded).toHaveLength(2);
    expect(engine.getState()).toBe('stopped');
    expect(engine.getPosition()).toBe(0);
  });

  it('loadTrack resolves the longest stem duration', async () => {
    const rig = makeRig();
    const duration = await rig.engine.loadTrack({ stems: [{ name: 'drums', url: '/drums' }], beatGrid: null });
    expect(duration).toBe(30);
  });

  it('a failing loadTrack rejects with the stem name and keeps the previous track', async () => {
    const rig = await loaded();
    await expect(rig.engine.loadTrack({ stems: [{ name: 'bass', url: '/missing' }], beatGrid: null })).rejects.toThrow(
      'Failed to load stem "bass": HTTP 404',
    );
    expect(() => {
      rig.engine.setChannel('vocals', { volume: 1, pan: 0, mute: false, solo: false });
    }).not.toThrow();
  });

  it('play connects every stem source to its mixer channel and starts them sample-locked', async () => {
    const { engine, ctx, ref } = await loaded();
    ctx.currentTime = 2;
    await engine.play();
    expect(ctx.sources.map((s) => s.startCalls)).toEqual([[{ when: 2.1, offset: 0 }], [{ when: 2.1, offset: 0 }]]);
    const targets = ctx.sources.map((s) => s.connections[0]);
    expect(new Set(targets).size).toBe(2);
    targets.forEach((node) => {
      expect(ctx.gains).toContain(node);
    });
    expect(engine.getState()).toBe('playing');
    expect(ref.argsOf('OnPlaybackStateChanged')).toEqual(['playing']);
    ctx.currentTime = 3.1;
    expect(engine.getPosition()).toBeCloseTo(1, 9);
  });

  it('reports the position to .NET at 10 Hz while playing', async () => {
    vi.useFakeTimers();
    const { engine, ctx, ref } = await loaded();
    await engine.play();
    ctx.currentTime = 0.6;
    vi.advanceTimersByTime(100);
    expect(ref.argsOf('OnPositionChanged')).toEqual([0.5]);
  });

  it('pause, seek and stop reach the transport and .NET', async () => {
    const { engine, ctx, ref } = await loaded();
    await engine.play();
    ctx.currentTime = 1.1;
    engine.pause();
    expect(engine.getState()).toBe('paused');
    engine.seek(12);
    expect(engine.getPosition()).toBe(12);
    engine.stop();
    expect(engine.getPosition()).toBe(0);
    expect(ref.argsOf('OnPositionChanged')).toEqual([1, 12, 0]);
    expect(() => {
      engine.seek(Number.NaN);
    }).toThrow('Invalid position');
  });

  it('setChannel and setMaster drive the mixer gains', async () => {
    const { engine, ctx } = await loaded();
    engine.setMaster(0.3);
    engine.setChannel('drums', { volume: 0.4, pan: 0.5, mute: false, solo: false });
    expect(ctx.gains[0]?.gain.value).toBe(0.3);
    expect(ctx.gains.map((g) => g.gain.value)).toContain(0.4);
    expect(ctx.panners.map((p) => p.pan.value)).toContain(0.5);
    expect(() => {
      engine.setChannel('piano', { volume: 1, pan: 0, mute: false, solo: false });
    }).toThrow('Unknown stem "piano"');
  });

  it('setLoop validates against the duration and applies to the sources', async () => {
    const { engine, ctx } = await loaded();
    expect(() => {
      engine.setLoop({ start: 1, end: 31, enabled: true });
    }).toThrow('Invalid loop region: ');
    engine.setLoop({ start: 1, end: 3, enabled: true });
    await engine.play();
    expect(ctx.sources.every((s) => s.loop && s.loopStart === 1 && s.loopEnd === 3)).toBe(true);
  });

  it('a new track clears the loop, stops playback and rebuilds the channels', async () => {
    const rig = await loaded();
    rig.engine.setLoop({ start: 1, end: 3, enabled: true });
    await rig.engine.play();
    await rig.engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
    expect(rig.engine.getState()).toBe('stopped');
    expect(() => {
      rig.engine.setChannel('drums', { volume: 1, pan: 0, mute: false, solo: false });
    }).toThrow('Unknown stem "drums"');
    await rig.engine.play();
    expect(rig.ctx.sources.at(-1)?.loop).toBe(false);
  });

  it('attachMeter draws into the element on animation frames; detachMeter stops the loop', async () => {
    const { engine, ctx } = await loaded();
    const element = document.createElement('div');
    element.id = 'meter-master';
    document.body.replaceChildren(element);
    const master = ctx.analysers[0];
    if (master !== undefined) {
      master.samples = [0.5];
    }
    engine.attachMeter('master', 'meter-master');
    frames[0]?.(16);
    expect(element.dataset.peakDb).toBe('-6');
    engine.detachMeter('master');
    expect(() => {
      engine.attachMeter('guitar', 'meter-guitar');
    }).toThrow('Unknown meter target "guitar"');
  });

  it('setMetronome stores valid settings (volume clamped) and rejects invalid ones', async () => {
    const { engine } = await loaded();
    const impl = engine as Engine;
    expect(impl.metronomeSettings.enabled).toBe(false);
    engine.setMetronome({ ...METRONOME, volume: 3, bpmOverride: 120 });
    expect(impl.metronomeSettings).toEqual({ ...METRONOME, volume: 1, bpmOverride: 120 });
    const invalid: Partial<MetronomeSettings>[] = [
      { bpmOverride: 20 },
      { bpmOverride: 301 },
      { offsetMs: 2001 },
      { offsetMs: -2001 },
      { beatsPerBar: 0 },
      { beatsPerBar: 13 },
      { beatsPerBar: 2.5 },
    ];
    for (const patch of invalid) {
      expect(() => {
        engine.setMetronome({ ...METRONOME, ...patch });
      }).toThrow('Invalid metronome settings: ');
    }
    expect(impl.metronomeSettings.bpmOverride).toBe(120);
  });

  it('remembers the beat grid of the loaded track', async () => {
    const rig = makeRig();
    const grid = { bpm: 120, beats: [0, 0.5], downbeats: [0], beatsPerBar: 4, confidence: 0.9, reliable: true };
    await rig.engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: grid });
    expect((rig.engine as Engine).beatGrid).toEqual(grid);
  });

  it('dispose stops playback silently, cancels timers and closes the context; every method then throws', async () => {
    vi.useFakeTimers();
    const { engine, ctx, ref } = await loaded();
    await engine.play();
    ref.clear();
    await engine.dispose();
    await engine.dispose();
    expect(ctx.state).toBe('closed');
    expect(ctx.sources.every((s) => s.stopCalls.length === 1)).toBe(true);
    vi.advanceTimersByTime(1000);
    expect(ref.calls).toEqual([]);
    const sync: (() => unknown)[] = [
      () => engine.getPosition(),
      () => engine.getState(),
      () => {
        engine.pause();
      },
      () => {
        engine.stop();
      },
      () => {
        engine.seek(1);
      },
      () => {
        engine.setChannel('vocals', { volume: 1, pan: 0, mute: false, solo: false });
      },
      () => {
        engine.setMaster(1);
      },
      () => {
        engine.setLoop({ start: 0, end: 1, enabled: true });
      },
      () => {
        engine.setMetronome(METRONOME);
      },
      () => {
        engine.attachMeter('master', 'x');
      },
      () => {
        engine.detachMeter('master');
      },
    ];
    for (const call of sync) {
      expect(call).toThrow('Audio engine has been disposed');
    }
    await expect(engine.play()).rejects.toThrow('Audio engine has been disposed');
    await expect(engine.loadTrack({ stems: [], beatGrid: null })).rejects.toThrow('Audio engine has been disposed');
  });

  it('a track finishing to load after dispose is discarded', async () => {
    const rig = makeRig();
    const pending = rig.engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
    await rig.engine.dispose();
    await expect(pending).rejects.toThrow('Audio engine has been disposed');
  });

  it('defaults to a 44.1 kHz AudioContext and the global fetch', async () => {
    const ctx = new MockAudioContext();
    const factory = vi.fn(function (this: unknown, options: AudioContextOptions) {
      expect(options).toEqual({ sampleRate: 44100 });
      return ctx;
    });
    const stub = new FetchStub().reply('/vocals', {});
    vi.stubGlobal('AudioContext', factory);
    vi.stubGlobal('fetch', stub.fetch);
    const engine = createEngine(new FakeDotNetRef());
    await engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
    expect(factory).toHaveBeenCalledTimes(1);
    expect(stub.requested).toEqual(['/vocals']);
  });
});
