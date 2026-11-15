import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createEngine, type AudioEngine, type KaraokeLine, type KaraokeLyrics } from '../../src/index';
import { MockAudioContext } from '../mocks/audio-context';
import { FakeDotNetRef } from '../mocks/dotnet-ref';
import { FetchStub } from '../mocks/fetch';
import { HOST_ID, LYRICS } from '../mocks/karaoke-rig';
import { stubLayout, stubResizeObserver } from '../mocks/layout';

interface Rig {
  readonly ctx: MockAudioContext;
  readonly ref: FakeDotNetRef;
  readonly engine: AudioEngine;
  readonly host: HTMLElement;
  readonly frames: FrameRequestCallback[];
}

async function makeRig(): Promise<Rig> {
  const frames: FrameRequestCallback[] = [];
  vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => frames.push(cb));
  vi.stubGlobal('cancelAnimationFrame', (handle: number) => {
    frames.splice(handle - 1, 1, () => undefined);
  });
  const host = document.createElement('div');
  host.id = HOST_ID;
  host.dataset.sync = 'word';
  document.body.replaceChildren(host);
  const ctx = new MockAudioContext();
  ctx.decodeDurations.set(100, 60);
  const ref = new FakeDotNetRef();
  const stub = new FetchStub().reply('/vocals', { bytes: 100 });
  const engine = createEngine(ref, { audioContextFactory: () => ctx.asAudioContext(), fetch: stub.fetch });
  await engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: null });
  return { ctx, ref, engine, host, frames };
}

/** Runs every frame requested so far that has not run yet (frames requested meanwhile wait). */
function flush(rig: Rig, from: { done: number }): void {
  const until = rig.frames.length;
  while (from.done < until) {
    const frame = rig.frames[from.done];
    from.done += 1;
    frame?.(from.done * 16);
  }
}

function lyricsWith(lines: readonly KaraokeLine[]): KaraokeLyrics {
  return { lines };
}

const LINE: KaraokeLine = { startMs: 1000, endMs: 2000, words: [{ text: 'one', startMs: 1000, endMs: 1500 }] };

describe('engine karaoke methods', () => {
  beforeEach(() => {
    stubLayout();
    stubResizeObserver();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('attachKaraoke builds the banner and draws one frame', async () => {
    const rig = await makeRig();
    rig.engine.attachKaraoke(HOST_ID, LYRICS, { offsetMs: 0, reducedMotion: false });
    expect(rig.host.querySelectorAll('.karaoke-line')).toHaveLength(3);
    expect(rig.frames).toHaveLength(1);
    flush(rig, { done: 0 });
    expect(rig.host.dataset.state).toBe('countdown');
    expect(rig.host.dataset.positionMs).toBe('0');
  });

  it('throws for an unknown host, invalid lyrics and an out-of-range offset', async () => {
    const rig = await makeRig();
    expect(() => {
      rig.engine.attachKaraoke('nope', LYRICS, { offsetMs: 0 });
    }).toThrow('Karaoke host not found: nope');
    expect(() => {
      rig.engine.attachKaraoke(HOST_ID, lyricsWith([{ ...LINE, endMs: 1000 }]), { offsetMs: 0 });
    }).toThrow('Invalid karaoke lyrics: ');
    expect(() => {
      rig.engine.attachKaraoke(HOST_ID, LYRICS, { offsetMs: 6000 });
    }).toThrow('Karaoke offset out of range: 6000');
    expect(() => {
      rig.engine.setKaraokeOffset(-5001);
    }).toThrow('Karaoke offset out of range: -5001');
    expect(rig.host.children).toHaveLength(0);
  });

  it('attachKaraoke replaces an attached karaoke; a failing attach keeps the old one', async () => {
    const rig = await makeRig();
    rig.engine.attachKaraoke(HOST_ID, LYRICS, { offsetMs: 0, reducedMotion: false });
    rig.engine.attachKaraoke(HOST_ID, lyricsWith([LINE]), { offsetMs: 0, reducedMotion: false });
    expect(rig.host.querySelectorAll('.karaoke-line')).toHaveLength(3);
    expect(rig.host.querySelectorAll('.karaoke-word')).toHaveLength(1);
    expect(() => {
      rig.engine.attachKaraoke(HOST_ID, lyricsWith([]), { offsetMs: 9999 });
    }).toThrow('out of range');
    expect(rig.host.querySelectorAll('.karaoke-word')).toHaveLength(1);
  });

  it('detachKaraoke removes the engine children and attributes, keeps Blazor attributes and stops drawing', async () => {
    const rig = await makeRig();
    rig.engine.detachKaraoke();
    rig.engine.attachKaraoke(HOST_ID, LYRICS, { offsetMs: 0, reducedMotion: false });
    const done = { done: 0 };
    flush(rig, done);
    rig.engine.detachKaraoke();
    rig.engine.detachKaraoke();
    expect(rig.host.children).toHaveLength(0);
    expect(Object.keys(rig.host.dataset)).toEqual(['sync']);
    rig.engine.seek(5);
    flush(rig, done);
    expect(rig.host.children).toHaveLength(0);
    expect(Object.keys(rig.host.dataset)).toEqual(['sync']);
  });

  it('setLoop while paused redraws one frame', async () => {
    const rig = await makeRig();
    rig.engine.attachKaraoke(HOST_ID, LYRICS, { offsetMs: 0, reducedMotion: false });
    const done = { done: 0 };
    flush(rig, done);
    rig.engine.setLoop({ start: 1, end: 2, enabled: true });
    expect(rig.frames.length - done.done).toBe(1);
  });

  it('plays without timers and without per-frame interop', async () => {
    vi.useFakeTimers();
    const rig = await makeRig();
    rig.engine.attachKaraoke(HOST_ID, LYRICS, { offsetMs: 0, reducedMotion: false });
    await rig.engine.play();
    const timeout = vi.spyOn(globalThis, 'setTimeout');
    const interval = vi.spyOn(globalThis, 'setInterval');
    rig.ref.clear();
    const done = { done: 0 };
    for (let i = 0; i < 50; i += 1) {
      rig.ctx.currentTime = 4 + i * 0.016;
      flush(rig, done);
    }
    expect(done.done).toBeGreaterThanOrEqual(50);
    expect(rig.host.dataset.state).toBe('singing');
    expect(timeout).not.toHaveBeenCalled();
    expect(interval).not.toHaveBeenCalled();
    expect(rig.ref.calls).toEqual([]);
  });

  it('dispose detaches the karaoke and later calls throw', async () => {
    const rig = await makeRig();
    rig.engine.attachKaraoke(HOST_ID, LYRICS, { offsetMs: 0, reducedMotion: false });
    await rig.engine.dispose();
    expect(rig.host.children).toHaveLength(0);
    expect(() => {
      rig.engine.attachKaraoke(HOST_ID, LYRICS, { offsetMs: 0 });
    }).toThrow('Audio engine has been disposed');
    expect(() => {
      rig.engine.setKaraokeOffset(0);
    }).toThrow('Audio engine has been disposed');
    expect(() => {
      rig.engine.detachKaraoke();
    }).toThrow('Audio engine has been disposed');
  });
});
