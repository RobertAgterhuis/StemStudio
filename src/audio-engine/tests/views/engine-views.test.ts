import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FakeStretchStage } from '../../src/stretch/fake-stage';
import { makeEngineRig, loadVocals } from '../mocks/engine-rig';
import { practice } from '../mocks/practice-rig';
import { GRID, addHost, cycleChart } from '../mocks/views-rig';

beforeEach(() => {
  document.body.replaceChildren();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('engine chord lane (2.6.0)', () => {
  it('attaches a lane with the loaded beat grid, follows seeks and detaches', async () => {
    const rig = makeEngineRig();
    await loadVocals(rig, GRID);
    const host = addHost('lane');
    rig.engine.attachChordLane('lane', cycleChart(), { reducedMotion: false });
    rig.step();
    expect(host.querySelectorAll('.chord-lane-item')).toHaveLength(8);
    rig.engine.seek(10);
    rig.step();
    expect(host.dataset.positionMs).toBe('10000');
    expect(host.dataset.currentSymbol).toBe('C');
    rig.engine.detachChordLane();
    expect(host.children).toHaveLength(0);
  });

  it('redraws the lane after a loop change and re-spells it after a transpose change', async () => {
    const rig = makeEngineRig({ timeStretchFactory: (context) => Promise.resolve(new FakeStretchStage(context)) });
    await loadVocals(rig, GRID);
    const host = addHost('lane');
    rig.engine.attachChordLane('lane', cycleChart(), { reducedMotion: false });
    rig.step();
    rig.engine.setLoop({ start: 1, end: 5, enabled: true });
    rig.step();
    expect(host.dataset.positionMs).toBe('0');
    expect(host.dataset.nextSymbol).toBe('C');
    await rig.engine.setPractice(practice({ transposeSemitones: 2 }));
    rig.step();
    expect(host.dataset.nextSymbol).toBe('D');
    expect(host.querySelectorAll('.chord-lane-item')[1]?.textContent).toBe('D');
  });
});

describe('engine banner chords and chord display (2.6.0)', () => {
  const lyrics = { lines: [{ startMs: 1000, endMs: 3000, words: [{ text: 'Fake', startMs: 1000, endMs: 2000 }, { text: 'line', startMs: 2000, endMs: 3000 }] }] };

  function bannerTexts(host: HTMLElement): string[] {
    return [...host.querySelectorAll('.karaoke-chord')].map((c) => c.textContent);
  }

  it('sets chords before attachKaraoke, re-spells them on a capo change and removes them with null', async () => {
    const rig = makeEngineRig();
    await loadVocals(rig);
    const host = addHost('karaoke-banner');
    rig.engine.setKaraokeChords(cycleChart());
    rig.engine.attachKaraoke('karaoke-banner', lyrics, { offsetMs: 0, reducedMotion: true });
    rig.step();
    expect(bannerTexts(host)).toEqual(['C']);
    rig.engine.setChordDisplay({ capo: 5 });
    rig.step();
    expect(bannerTexts(host)).toEqual(['G']);
    rig.engine.setKaraokeChords(null);
    rig.step();
    expect(bannerTexts(host)).toEqual([]);
  });

  it('rejects a capo out of range without changing the display', async () => {
    const rig = makeEngineRig();
    await loadVocals(rig);
    const host = addHost('karaoke-banner');
    rig.engine.setKaraokeChords(cycleChart());
    rig.engine.attachKaraoke('karaoke-banner', lyrics, { offsetMs: 0, reducedMotion: true });
    expect(() => {
      rig.engine.setChordDisplay({ capo: 13 });
    }).toThrow('Capo out of range: 13');
    rig.step();
    expect(bannerTexts(host)).toEqual(['C']);
  });
});
