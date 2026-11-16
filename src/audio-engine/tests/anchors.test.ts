import { describe, expect, it } from 'vitest';
import { AnchorQueue, anchorOf, rateOf, trackAt } from '../src/anchors';

describe('anchors', () => {
  it('omits the rate at 1 and reads it back as 1', () => {
    expect(anchorOf(1, 2, 1)).toEqual({ when: 1, offset: 2 });
    expect(anchorOf(1, 2, 0.5)).toEqual({ when: 1, offset: 2, rate: 0.5 });
    expect(rateOf({ when: 0, offset: 0 })).toBe(1);
  });

  it('maps context time to track time, clamped before when and after until', () => {
    const anchor = { when: 10, offset: 2, rate: 0.5, until: 12 };
    expect(trackAt(anchor, 9)).toBe(2);
    expect(trackAt(anchor, 11)).toBe(2.5);
    expect(trackAt(anchor, 20)).toBe(3);
  });

  it('keeps anchors in when order and never removes the first', () => {
    const first = { when: 0, offset: 0 };
    const queue = new AnchorQueue(first);
    const late = { when: 5, offset: 5 };
    const middle = { when: 3, offset: 3 };
    queue.push(late);
    queue.push(middle);
    expect(queue.all).toEqual([first, middle, late]);
    queue.remove(middle);
    queue.remove(first);
    expect(queue.all).toEqual([first, late]);
    expect(queue.at(-1)).toBe(first);
    expect(queue.at(4)).toBe(first);
    expect(queue.at(5)).toBe(late);
  });

  it('returns the next pass start inside a gap after until', () => {
    const queue = new AnchorQueue({ when: 0, offset: 2, until: 1 });
    expect(queue.positionAt(1.2)).toBe(3);
    queue.push({ when: 1.5, offset: 2 });
    expect(queue.positionAt(0.5)).toBe(2.5);
    expect(queue.positionAt(1.2)).toBe(2);
    expect(queue.positionAt(1.7)).toBeCloseTo(2.2, 9);
  });

  it('prunes anchors whose successor started before the limit', () => {
    const queue = new AnchorQueue({ when: 0, offset: 0 });
    [1, 2, 3, 4].forEach((t) => {
      queue.push({ when: t, offset: t });
    });
    queue.prune(2.5);
    expect(queue.all.map((a) => a.when)).toEqual([2, 3, 4]);
    queue.prune(10);
    expect(queue.all.map((a) => a.when)).toEqual([4]);
    queue.prune(10);
    expect(queue.all).toHaveLength(1);
  });
});
