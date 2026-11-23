import { describe, expect, it } from 'vitest';
import { Cursor, lastAtOrBefore } from '../../src/timeline/cursor';

describe('timeline Cursor (shared by every view)', () => {
  const starts = Array.from({ length: 4000 }, (_, i) => i * 250);

  it('advances in O(1) per frame during playback (no search)', () => {
    const cursor = new Cursor(starts);
    for (let t = 0; t < 60_000; t += 16) {
      cursor.index(t);
    }
    expect(cursor.searches).toBe(0);
  });

  it('binary-searches once after a seek and once after a loop wrap', () => {
    const cursor = new Cursor(starts);
    expect(cursor.index(100)).toBe(0);
    expect(cursor.index(500_000)).toBe(2000);
    expect(cursor.searches).toBe(1);
    expect(cursor.index(500_016)).toBe(2000);
    expect(cursor.index(2000)).toBe(8);
    expect(cursor.searches).toBe(2);
    expect(lastAtOrBefore(starts, -1)).toBe(-1);
  });
});
