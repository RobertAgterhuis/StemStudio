import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Cursor, lastAtOrBefore } from '../../src/karaoke/cursor';
import { attached, attr, frameAt, play, showAt } from '../mocks/karaoke-rig';
import { stubLayout, stubResizeObserver } from '../mocks/layout';

describe('lastAtOrBefore (binary search)', () => {
  const starts = [10, 20, 20, 30];

  it('returns the last index whose start is <= t, -1 before the first', () => {
    expect(lastAtOrBefore([], 5)).toBe(-1);
    expect(lastAtOrBefore(starts, 9)).toBe(-1);
    expect(lastAtOrBefore(starts, 10)).toBe(0);
    expect(lastAtOrBefore(starts, 19)).toBe(0);
    expect(lastAtOrBefore(starts, 20)).toBe(2);
    expect(lastAtOrBefore(starts, 29)).toBe(2);
    expect(lastAtOrBefore(starts, 30)).toBe(3);
    expect(lastAtOrBefore(starts, 1e9)).toBe(3);
  });
});

describe('Cursor', () => {
  const starts = Array.from({ length: 1000 }, (_, i) => i * 10);

  it('advances forward in O(1): no binary search during normal playback', () => {
    const cursor = new Cursor(starts);
    for (let t = -50; t < 10_000; t += 3) {
      expect(cursor.index(t)).toBe(t < 0 ? -1 : Math.floor(t / 10));
    }
    expect(cursor.searches).toBe(0);
  });

  it('binary-searches after a backward jump (seek back, loop wrap) and a forward jump', () => {
    const cursor = new Cursor(starts);
    expect(cursor.index(5000)).toBe(500);
    expect(cursor.searches).toBe(1);
    expect(cursor.index(1234)).toBe(123);
    expect(cursor.searches).toBe(2);
    expect(cursor.index(1240)).toBe(124);
    expect(cursor.index(1255)).toBe(125);
    expect(cursor.searches).toBe(2);
    expect(cursor.index(-1)).toBe(-1);
    expect(cursor.searches).toBe(3);
  });

  it('handles equal starts and an empty list', () => {
    const cursor = new Cursor([0, 100, 100, 200]);
    expect(cursor.index(100)).toBe(2);
    expect(cursor.index(150)).toBe(2);
    expect(cursor.index(250)).toBe(3);
    expect(cursor.index(1e6)).toBe(3);
    expect(new Cursor([]).index(10)).toBe(-1);
  });
});

describe('karaoke cursor after seek, loop wrap and offset change', () => {
  beforeEach(() => {
    stubLayout();
    stubResizeObserver();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('finds the right line and word after a backward seek and a forward seek', () => {
    const rig = attached();
    showAt(rig, 15_600);
    expect([attr(rig, 'data-line-index'), attr(rig, 'data-word-index')]).toEqual(['2', '1']);
    showAt(rig, 4450);
    expect([attr(rig, 'data-line-index'), attr(rig, 'data-word-index')]).toEqual(['0', '1']);
    showAt(rig, 6700);
    expect([attr(rig, 'data-line-index'), attr(rig, 'data-word-index')]).toEqual(['1', '2']);
  });

  it('follows a loop wrap back to the loop start', async () => {
    const rig = attached();
    await play(rig);
    rig.transport.setLoop({ start: 4, end: 7, enabled: true });
    rig.ctx.currentTime = 0;
    rig.transport.seek(6.9);
    frameAt(rig, 90);
    expect(attr(rig, 'data-position-ms')).toBe('6990');
    expect(attr(rig, 'data-word-index')).toBe('2');
    frameAt(rig, 150);
    expect(attr(rig, 'data-position-ms')).toBe('4050');
    expect([attr(rig, 'data-line-index'), attr(rig, 'data-word-index')]).toEqual(['0', '0']);
  });

  it('re-locates after an offset change', () => {
    const rig = attached();
    showAt(rig, 6550);
    expect([attr(rig, 'data-line-index'), attr(rig, 'data-word-index')]).toEqual(['1', '1']);
    rig.karaoke.setOffset(2000);
    rig.frames.step(1);
    expect(attr(rig, 'data-position-ms')).toBe('4550');
    expect([attr(rig, 'data-line-index'), attr(rig, 'data-word-index')]).toEqual(['0', '1']);
    rig.karaoke.setOffset(-5000);
    rig.frames.step(2);
    expect(attr(rig, 'data-position-ms')).toBe('11550');
    expect([attr(rig, 'data-line-index'), attr(rig, 'data-word-index')]).toEqual(['1', '-1']);
  });
});
