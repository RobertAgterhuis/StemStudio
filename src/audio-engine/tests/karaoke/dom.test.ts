import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { KaraokeLyrics } from '../../src/types';
import {
  attached,
  attr,
  frameAt,
  part,
  play,
  showAt,
  wordStates,
  wordTexts,
  type KaraokeRig,
} from '../mocks/karaoke-rig';
import { stubLayout, stubResizeObserver } from '../mocks/layout';

function host(rig: KaraokeRig): Record<string, string | null> {
  return {
    state: attr(rig, 'data-state'),
    line: attr(rig, 'data-line-index'),
    word: attr(rig, 'data-word-index'),
    position: attr(rig, 'data-position-ms'),
  };
}

function dots(rig: KaraokeRig): string | undefined {
  return part(rig, '.karaoke-countdown').dataset.count;
}

describe('karaoke DOM', () => {
  beforeEach(() => {
    stubLayout();
    stubResizeObserver();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('builds three slot lines, one ball and a three-dot countdown inside the host', () => {
    const rig = attached();
    const slots = [...rig.host.querySelectorAll<HTMLElement>('.karaoke-line')].map((l) => l.dataset.slot);
    expect(slots).toEqual(['previous', 'current', 'next']);
    expect(rig.host.querySelectorAll('.karaoke-ball')).toHaveLength(1);
    expect(rig.host.querySelectorAll('.karaoke-countdown')).toHaveLength(1);
    expect(part(rig, '.karaoke-countdown').children).toHaveLength(3);
    expect(wordTexts(rig, 'previous')).toEqual([]);
    expect(wordTexts(rig, 'current')).toEqual(['Hold', 'the', 'line']);
    expect(wordTexts(rig, 'next')).toEqual(['Keep', 'on', 'going']);
  });

  it('sets word text with textContent only (markup stays literal text)', () => {
    const lyrics: KaraokeLyrics = {
      lines: [{ startMs: 0, endMs: 1000, words: [{ text: '<b onclick="x()">bold</b>', startMs: 0, endMs: 1000 }] }],
    };
    const rig = attached(lyrics);
    const word = part(rig, '.karaoke-word');
    expect(word.textContent).toBe('<b onclick="x()">bold</b>');
    expect(word.children).toHaveLength(0);
    expect(rig.host.querySelector('b')).toBeNull();
  });

  it('before the first line: countdown, no line or word, dots only in the last 3 s', () => {
    const rig = attached();
    expect(host(rig)).toEqual({ state: 'countdown', line: '-1', word: '-1', position: '0' });
    expect(dots(rig)).toBe('0');
    expect(wordStates(rig, 'current')).toEqual(['future', 'future', 'future']);
    showAt(rig, 1000);
    expect(dots(rig)).toBe('3');
    showAt(rig, 2000);
    expect(dots(rig)).toBe('2');
    showAt(rig, 3999);
    expect(dots(rig)).toBe('1');
  });

  it('singing: past/current/future words and host indices', () => {
    const rig = attached();
    showAt(rig, 4450);
    expect(host(rig)).toEqual({ state: 'singing', line: '0', word: '1', position: '4450' });
    expect(wordStates(rig, 'current')).toEqual(['past', 'current', 'future']);
    expect(wordStates(rig, 'next')).toEqual(['future', 'future', 'future']);
    expect(dots(rig)).toBe('0');
  });

  it('keeps the last word current until the line ends, then the next line moves into the current slot', () => {
    const rig = attached();
    showAt(rig, 5800);
    expect(wordStates(rig, 'current')).toEqual(['past', 'past', 'current']);
    showAt(rig, 6000);
    expect(host(rig)).toEqual({ state: 'singing', line: '1', word: '0', position: '6000' });
    expect(wordTexts(rig, 'previous')).toEqual(['Hold', 'the', 'line']);
    expect(wordStates(rig, 'previous')).toEqual(['past', 'past', 'past']);
    expect(wordTexts(rig, 'current')).toEqual(['Keep', 'on', 'going']);
    expect(wordStates(rig, 'current')).toEqual(['current', 'future', 'future']);
    expect(wordTexts(rig, 'next')).toEqual(['After', 'gap']);
  });

  it('a gap longer than 5 s shows the countdown from 3 s before the next line', () => {
    const rig = attached();
    showAt(rig, 9000);
    expect(host(rig)).toEqual({ state: 'singing', line: '1', word: '-1', position: '9000' });
    expect(wordStates(rig, 'current')).toEqual(['past', 'past', 'past']);
    showAt(rig, 12_000);
    expect(attr(rig, 'data-state')).toBe('countdown');
    expect(dots(rig)).toBe('3');
    showAt(rig, 14_500);
    expect(dots(rig)).toBe('1');
    showAt(rig, 15_000);
    expect(attr(rig, 'data-state')).toBe('singing');
    expect(wordTexts(rig, 'next')).toEqual([]);
  });

  it('a shorter gap or a longer countdownGapMs shows no countdown', () => {
    const rig = attached(undefined, { countdownGapMs: 7000 });
    showAt(rig, 13_000);
    expect(attr(rig, 'data-state')).toBe('singing');
  });

  it('is ended after the last line and hidden without lines', () => {
    const rig = attached();
    showAt(rig, 17_000);
    expect(host(rig)).toEqual({ state: 'ended', line: '2', word: '-1', position: '17000' });
    const empty = attached({ lines: [] });
    expect(host(empty)).toEqual({ state: 'hidden', line: '-1', word: '-1', position: '0' });
    expect(part(empty, '.karaoke-ball').style.transform).toBe('');
  });

  it('per steady frame writes only the ball transform and data-position-ms', async () => {
    const rig = attached();
    await play(rig);
    frameAt(rig, 4410);
    const observer = new MutationObserver(() => undefined);
    observer.observe(rig.host, { attributes: true, subtree: true, childList: true });
    frameAt(rig, 4427);
    const steady = observer.takeRecords().map((r) => `${(r.target as HTMLElement).className || 'host'}:${r.attributeName ?? r.type}`);
    expect(steady.sort()).toEqual(['host:data-position-ms', 'karaoke-ball:style']);
    frameAt(rig, 4605);
    const change = observer.takeRecords().map((r) => `${(r.target as HTMLElement).className || 'host'}:${r.attributeName ?? r.type}`);
    expect(change.sort()).toEqual([
      'host:data-position-ms',
      'host:data-word-index',
      'karaoke-ball:style',
      'karaoke-word:data-state',
      'karaoke-word:data-state',
    ]);
    observer.disconnect();
  });
});

describe('karaoke redraws', () => {
  beforeEach(() => {
    stubLayout();
    stubResizeObserver();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('draws continuously only while playing, and exactly one frame after seek, pause, stop, loop and offset changes', async () => {
    const rig = attached();
    expect(rig.frames.size).toBe(0);
    rig.transport.seek(5);
    expect(rig.frames.size).toBe(1);
    rig.frames.step(1);
    expect(rig.frames.size).toBe(0);
    rig.karaoke.setOffset(100);
    expect(rig.frames.size).toBe(1);
    rig.frames.step(2);
    expect(rig.frames.size).toBe(0);
    await play(rig);
    rig.frames.step(3);
    rig.frames.step(4);
    expect(rig.frames.size).toBe(1);
    rig.transport.pause();
    rig.frames.step(5);
    expect(rig.frames.size).toBe(0);
    rig.karaoke.redraw();
    expect(rig.frames.size).toBe(1);
    rig.frames.step(6);
    rig.transport.stop();
    expect(rig.frames.size).toBe(1);
    rig.frames.step(7);
    expect(attr(rig, 'data-position-ms')).toBe('-100');
    expect(rig.frames.size).toBe(0);
  });

  it('transport events without an attached karaoke request no frame', () => {
    const rig = attached();
    rig.karaoke.detach();
    rig.transport.seek(5);
    rig.karaoke.onStateChanged('playing');
    rig.karaoke.setOffset(10);
    rig.karaoke.redraw();
    expect(rig.frames.size).toBe(0);
  });
});
