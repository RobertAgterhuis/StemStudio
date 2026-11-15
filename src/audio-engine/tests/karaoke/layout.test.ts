import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { measureSlots } from '../../src/karaoke/layout';
import { attached, frameAt, part, play, showAt, type KaraokeRig } from '../mocks/karaoke-rig';
import { FakeResizeObserver, LAYOUT, layoutShift, rowY, stubLayout, stubResizeObserver, wordX } from '../mocks/layout';

function transform(rig: KaraokeRig): string {
  return part(rig, '.karaoke-ball').style.transform;
}

function slotWords(rig: KaraokeRig): HTMLElement[] {
  return [...rig.host.querySelectorAll<HTMLElement>('.karaoke-word')];
}

describe('measureSlots', () => {
  beforeEach(() => {
    stubLayout();
  });

  it('returns word centres and tops relative to the host and the current line height', () => {
    const rig = attached();
    const slot = (name: string, line: number): { line: number; element: HTMLElement } => ({
      line,
      element: part(rig, `.karaoke-line[data-slot="${name}"]`),
    });
    const geometry = measureSlots(rig.host, [slot('previous', -1), slot('current', 0), slot('next', 1)]);
    expect(geometry.lineHeight).toBe(LAYOUT.rowHeight);
    expect(geometry.lines.get(-1)).toEqual([]);
    expect(geometry.lines.get(0)).toEqual([0, 1, 2].map((i) => ({ x: wordX(i), y: rowY('current') })));
    expect(geometry.lines.get(1)?.[0]).toEqual({ x: wordX(0), y: rowY('next') });
  });
});

describe('karaoke layout', () => {
  beforeEach(() => {
    stubResizeObserver();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('measures once after building the DOM and never inside a frame callback', async () => {
    const reads = stubLayout();
    const rig = attached();
    expect(reads.mock.calls.length).toBeGreaterThan(0);
    await play(rig);
    reads.mockClear();
    for (let t = 3000; t < 17_000; t += 16) {
      frameAt(rig, t);
    }
    expect(reads).not.toHaveBeenCalled();
  });

  it('observes the host and the words of the three slots, and re-observes the new words after a line change', () => {
    stubLayout();
    const rig = attached();
    const observer = FakeResizeObserver.latest();
    expect([...observer.observed]).toEqual([rig.host, ...slotWords(rig)]);
    const before = slotWords(rig);
    showAt(rig, 6000);
    expect(observer.observed.has(rig.host)).toBe(true);
    expect(slotWords(rig).every((w) => observer.observed.has(w))).toBe(true);
    expect(before.some((w) => observer.observed.has(w))).toBe(false);
  });

  it('after a line change the ball waits for the ResizeObserver measurement, then lands in the same frame', () => {
    stubLayout();
    const rig = attached();
    showAt(rig, 5999);
    const stale = transform(rig);
    showAt(rig, 6000);
    expect(transform(rig)).toBe(stale);
    FakeResizeObserver.latest().trigger();
    expect(transform(rig)).toBe(`translate3d(${String(wordX(0))}px, ${String(rowY('current'))}px, 0px)`);
  });

  it('re-measures on a resize, redraws the ball at once and draws one frame', () => {
    stubLayout();
    const rig = attached();
    showAt(rig, 4000);
    layoutShift.x = 30;
    FakeResizeObserver.latest().trigger();
    expect(transform(rig)).toBe(`translate3d(${String(wordX(0))}px, ${String(rowY('current'))}px, 0px)`);
    expect(wordX(0)).toBe(55);
    expect(rig.frames.size).toBe(1);
  });

  it('without ResizeObserver it measures right after building the slots', () => {
    vi.stubGlobal('ResizeObserver', undefined);
    stubLayout();
    const rig = attached();
    showAt(rig, 6000);
    expect(transform(rig)).toBe(`translate3d(${String(wordX(0))}px, ${String(rowY('current'))}px, 0px)`);
  });

  it('detach disconnects the observer', () => {
    stubLayout();
    const rig = attached();
    rig.karaoke.detach();
    expect(FakeResizeObserver.latest().disconnected).toBe(true);
  });
});
