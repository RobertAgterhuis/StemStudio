import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { bannerChords } from '../../src/chords/banner';
import { ChordDisplayState } from '../../src/chords/display';
import type { ChordChartData, ChordSymbolEvent } from '../../src/types';
import { HOST_ID, LYRICS, attached, frameAt, makeKaraoke, play, showAt, type KaraokeRig } from '../mocks/karaoke-rig';
import { stubLayout, stubResizeObserver } from '../mocks/layout';

function seg(startMs: number, endMs: number, root: number | null, quality: ChordSymbolEvent['quality'] = 'maj'): ChordSymbolEvent {
  return { startMs, endMs, root, quality: root === null ? null : quality, bass: null };
}

/** C is a lead chord of line 0, G sits on "the", Am on "Keep", F is a lead of line 2, the last C on "After". */
const CHART: ChordChartData = {
  segments: [seg(0, 3000, null), seg(3000, 4500, 0), seg(4500, 6000, 7), seg(6000, 9000, 9, 'min'), seg(9000, 15_200, 5), seg(15_200, 20_000, 0), seg(20_000, 30_000, 2)],
  keyTonic: 0,
  keyMode: 'major',
};

interface Chord {
  readonly text: string;
  readonly state: string | undefined;
  readonly lead: boolean;
  readonly before: string | null | undefined;
}

/** Text of the next word, or the class of the next element when it is no word. */
function nextOf(element: HTMLElement): string | null | undefined {
  const next = element.nextElementSibling;
  return next?.classList.contains('karaoke-word') === true ? next.textContent : next?.className;
}

function chords(rig: KaraokeRig, slot: 'previous' | 'current' | 'next'): Chord[] {
  const elements = rig.host.querySelectorAll<HTMLElement>(`.karaoke-line[data-slot="${slot}"] .karaoke-chord`);
  return [...elements].map((c) => ({
    text: String(c.firstChild?.textContent),
    state: c.dataset.state,
    lead: c.dataset.lead === 'true',
    before: nextOf(c),
  }));
}

function texts(rig: KaraokeRig, slot: 'previous' | 'current' | 'next'): string[] {
  return chords(rig, slot).map((c) => c.text);
}

let display: ChordDisplayState;

function withChords(chart: ChordChartData | null = CHART): KaraokeRig {
  const rig = attached();
  rig.karaoke.setChords(chart === null ? null : bannerChords(chart, display));
  rig.frames.step(1);
  return rig;
}

beforeEach(() => {
  stubLayout();
  stubResizeObserver();
  display = new ChordDisplayState();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('karaoke chords anchoring', () => {
  it('puts each chord before the word whose [start, nextStart) holds its start; lead chords before the first word', () => {
    const rig = withChords();
    expect(chords(rig, 'current')).toEqual([
      { text: 'C', state: 'future', lead: true, before: 'Hold' },
      { text: 'G', state: 'future', lead: false, before: 'the' },
    ]);
    expect(chords(rig, 'next')).toEqual([{ text: 'Am', state: 'future', lead: false, before: 'Keep' }]);
  });

  it('makes a chord that starts after a line ended a lead chord of the next line', () => {
    const rig = withChords();
    showAt(rig, 15_000);
    expect(chords(rig, 'current').map((c) => [c.text, c.lead, c.before])).toEqual([
      ['F', true, 'karaoke-chord'],
      ['C', false, 'After'],
    ]);
  });

  it('shows no N.C. and no chord after the last line', () => {
    const rig = withChords();
    showAt(rig, 15_000);
    const all = [...rig.host.querySelectorAll('.karaoke-chord')].map((c) => c.firstChild?.textContent);
    expect(all).not.toContain('N.C.');
    expect(all).not.toContain('D');
  });

  it('keeps the word texts and the word order of the slots', () => {
    const rig = withChords();
    const words = [...rig.host.querySelectorAll('.karaoke-line[data-slot="current"] .karaoke-word')].map((w) => w.textContent);
    expect(words).toEqual(['Hold', 'the', 'line']);
  });
});

describe('karaoke chords states', () => {
  it('marks past, current and future against the karaoke clock and writes data-chord-index', async () => {
    const rig = withChords();
    await play(rig);
    frameAt(rig, 4500);
    expect(chords(rig, 'current').map((c) => c.state)).toEqual(['past', 'current']);
    expect(rig.host.dataset.chordIndex).toBe('2');
    frameAt(rig, 6100);
    expect(chords(rig, 'current').map((c) => c.state)).toEqual(['current']);
    expect(chords(rig, 'previous').map((c) => c.state)).toEqual(['past', 'past']);
  });

  it('writes -1 between chords of an empty chart', () => {
    const rig = withChords({ segments: [], keyTonic: null, keyMode: null });
    expect(rig.host.querySelectorAll('.karaoke-chord')).toHaveLength(0);
    expect(rig.host.dataset.chordIndex).toBe('-1');
  });
});

describe('setKaraokeChords lifecycle', () => {
  it('may come before attachKaraoke and is kept across attachKaraoke calls', () => {
    const rig = makeKaraoke();
    rig.karaoke.setChords(bannerChords(CHART, display));
    rig.karaoke.attach(HOST_ID, LYRICS, { offsetMs: 0, reducedMotion: false });
    rig.frames.step(0);
    expect(texts(rig, 'current')).toEqual(['C', 'G']);
    rig.karaoke.attach(HOST_ID, LYRICS, { offsetMs: 0, reducedMotion: false });
    rig.frames.step(1);
    expect(texts(rig, 'current')).toEqual(['C', 'G']);
  });

  it('null removes the chord elements and the host attribute', () => {
    const rig = withChords();
    rig.karaoke.setChords(null);
    rig.frames.step(2);
    expect(rig.host.querySelectorAll('.karaoke-chord')).toHaveLength(0);
    expect(rig.host.dataset.chordIndex).toBeUndefined();
  });

  it('replaces the chords of an attached banner', () => {
    const rig = withChords();
    rig.karaoke.setChords(bannerChords({ ...CHART, segments: [seg(0, 30_000, 2, 'min')] }, display));
    rig.frames.step(2);
    expect(texts(rig, 'current')).toEqual(['Dm']);
  });

  it('validates the chart (Invalid chord chart)', () => {
    expect(() => bannerChords({ ...CHART, segments: [seg(0, 10, 13)] }, display)).toThrow('Invalid chord chart: root outside 0..11');
  });

  it('detachKaraoke removes the chords with the banner; the chords stay for the next attach', () => {
    const rig = withChords();
    rig.karaoke.detach();
    expect(rig.host.dataset.chordIndex).toBeUndefined();
    rig.karaoke.attach(HOST_ID, LYRICS, { offsetMs: 0, reducedMotion: false });
    rig.frames.step(3);
    expect(texts(rig, 'current')).toEqual(['C', 'G']);
  });
});

describe('karaoke chords display (setChordDisplay, 012 transpose)', () => {
  it('re-spells on the next frame after a capo change', () => {
    const rig = withChords();
    display.set({ capo: 3 });
    rig.karaoke.redraw();
    rig.frames.step(2);
    expect(texts(rig, 'current')).toEqual(['A', 'E']);
  });

  it('re-spells on the next frame after a transpose change', () => {
    const rig = withChords();
    display.setTranspose(-2);
    rig.karaoke.redraw();
    rig.frames.step(2);
    expect(texts(rig, 'current')).toEqual(['Bb', 'F']);
    expect(texts(rig, 'next')).toEqual(['Gm']);
  });
});
