import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { FrameLoop } from '../src/frame-loop';
import { createEngine, type ScoreViewBar, type ScoreViewMap } from '../src/index';
import { BarCursor } from '../src/score-follow/cursor';
import { ScoreFollower } from '../src/score-follow/follower';
import { validateScoreView } from '../src/score-follow/validate';
import { MockAudioContext } from './mocks/audio-context';
import { FakeDotNetRef } from './mocks/dotnet-ref';
import { FakeFrames } from './mocks/frames';
import { makeTransport, type TransportRig } from './mocks/transport-rig';

/** Score follower (contract 2.4.0 "Score follower", 014 FR-010, FR-011, SC-003, SC-004). */
const HOST = 'score-view';

/** 8 bars of 2 s on 2 pages; a new system (y + 0.25) every 2 bars. */
function bar(index: number): ScoreViewBar {
  const page = index < 4 ? 1 : 2;
  const inPage = index % 4;
  return { index, number: index + 1, page, x: 0.1 + (inPage % 2) * 0.4, y: 0.1 + Math.floor(inPage / 2) * 0.25, width: 0.4, height: 0.2, startSeconds: 2 + index * 2, endSeconds: 4 + index * 2 };
}

const VIEW: ScoreViewMap = {
  pages: [1, 2].map((page) => ({ page, url: `/api/assets/a/score-view/pages/${String(page)}?v=1`, widthPx: 840, heightPx: 1188 })),
  bars: Array.from({ length: 8 }, (_, i) => bar(i)),
};

interface Rig extends TransportRig {
  readonly frames: FakeFrames;
  readonly loop: FrameLoop;
  readonly follower: ScoreFollower;
  readonly host: HTMLElement;
  readonly scrolls: { readonly page: string | undefined; readonly options: ScrollIntoViewOptions | undefined }[];
  nowMs: number;
  /** Visible: the result of the reduced-motion visibility check. */
  visible: boolean;
}

function makeHost(): HTMLElement {
  const host = document.createElement('div');
  host.id = HOST;
  [1, 2].forEach((page) => {
    const container = document.createElement('div');
    container.dataset.scorePage = String(page);
    container.appendChild(document.createElement('img'));
    host.appendChild(container);
  });
  document.body.replaceChildren(host);
  return host;
}

function makeRig(): Rig {
  const base = makeTransport();
  const host = makeHost();
  const frames = new FakeFrames();
  const loop = new FrameLoop({ frames, document });
  const rig = { ...base, frames, loop, host, scrolls: [], nowMs: 0, visible: true } as unknown as Rig;
  const follower = new ScoreFollower({
    loop,
    position: () => base.transport.positionAt(base.ctx.currentTime),
    playing: () => base.transport.state === 'playing',
    now: () => rig.nowMs,
    document,
    isVisible: () => rig.visible,
  });
  base.transport.addListener(follower);
  Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
    configurable: true,
    writable: true,
    value: function scroll(this: HTMLElement, options?: ScrollIntoViewOptions) {
      rig.scrolls.push({ page: this.parentElement?.dataset.scorePage, options });
    },
  });
  return Object.assign(rig, { follower });
}

function attached(options = { follow: true, reducedMotion: false }): Rig {
  const rig = makeRig();
  rig.follower.attach(HOST, VIEW, options);
  rig.frames.step(0);
  return rig;
}

/** Sets the clock and draws one frame. */
function frameAt(rig: Rig, time: number): void {
  rig.ctx.currentTime = time;
  rig.loop.once(() => undefined);
  rig.frames.step(time * 1000);
}

function highlight(rig: Rig, page: number): HTMLElement {
  const element = rig.host.querySelector<HTMLElement>(`[data-score-page="${String(page)}"] .score-highlight`);
  if (element === null) {
    throw new Error(`no highlight on page ${String(page)}`);
  }
  return element;
}

/** `data-state` of the highlight of each page. */
function pageStates(rig: Rig): (string | undefined)[] {
  return [1, 2].map((page) => highlight(rig, page).dataset.state);
}

function current(rig: Rig): string | undefined {
  return rig.host.dataset.barIndex;
}

beforeEach(() => {
  document.body.replaceChildren();
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('BarCursor', () => {
  const cursor = (): BarCursor => new BarCursor(VIEW.bars);

  it('finds the bar with start <= t < end; none before the first or after the last', () => {
    const c = cursor();
    expect([1.999, 2, 3.999, 4, 17.999, 18, 100].map((t) => c.locate(t))).toEqual([-1, 0, 0, 1, 7, -1, -1]);
  });

  it('steps forward frame by frame and binary-searches after a jump', () => {
    const c = cursor();
    expect([2, 4.1, 6.2, 16.5, 3].map((t) => c.locate(t))).toEqual([0, 1, 2, 7, 0]);
    expect(c.searches).toBe(2);
  });
});

describe('validateScoreView', () => {
  const pages = VIEW.pages;
  const bars = VIEW.bars;

  it.each([
    [{ pages: [], bars }, 'pages must be 1..200'],
    [{ pages: [pages[0], pages[0]], bars }, 'duplicate page 1'],
    [{ pages, bars: [] }, 'bars must be 1..2000'],
    [{ pages, bars: [{ ...bar(0), page: 3 }] }, 'bar 0 is on unknown page 3'],
    [{ pages, bars: [{ ...bar(0), x: 1.2 }] }, 'bar 0 box outside 0..1'],
    [{ pages, bars: [{ ...bar(0), width: 0 }] }, 'bar 0 box outside 0..1'],
    [{ pages, bars: [{ ...bar(0), y: Number.NaN }] }, 'bar 0 box outside 0..1'],
    [{ pages, bars: [{ ...bar(0), endSeconds: 2 }] }, 'bar 0 ends before it starts'],
    [{ pages, bars: [bar(0), { ...bar(1), startSeconds: 4.002 }] }, 'bar 1 does not follow bar 0'],
  ])('rejects %#', (view, reason) => {
    expect(() => {
      validateScoreView(view as ScoreViewMap);
    }).toThrow(`Invalid score view: ${reason}`);
  });

  it('accepts contiguity within 1 ms', () => {
    expect(() => {
      validateScoreView({ pages, bars: [bar(0), { ...bar(1), startSeconds: 4.0009 }] });
    }).not.toThrow();
  });
});

describe('ScoreFollower', () => {
  it('adds one aria-hidden highlight per page container and the host attributes', () => {
    const rig = attached();
    expect([1, 2].map((p) => highlight(rig, p).getAttribute('aria-hidden'))).toEqual(['true', 'true']);
    expect([current(rig), rig.host.dataset.barNumber, rig.host.dataset.page, rig.host.dataset.positionMs]).toEqual(['-1', '', '', '0']);
    expect(highlight(rig, 1).dataset.state).toBe('idle');
  });

  it('throws for an unknown host and for invalid options', () => {
    const rig = makeRig();
    expect(() => {
      rig.follower.attach('nope', VIEW, { follow: true });
    }).toThrow('Score host not found: nope');
    expect(() => {
      rig.follower.attach(HOST, VIEW, { follow: true, scrollPauseMs: 60001 });
    }).toThrow('Invalid score follow options: scrollPauseMs must be 0..60000');
  });

  it('highlights the bar of the heard position at its boundaries, in percent of the page', async () => {
    const rig = attached();
    await rig.transport.play();
    frameAt(rig, 0.1 + 1.999);
    expect(current(rig)).toBe('-1');
    frameAt(rig, 0.1 + 2);
    expect([current(rig), rig.host.dataset.barNumber, rig.host.dataset.page]).toEqual(['0', '1', '1']);
    const { left, top, width, height } = highlight(rig, 1).style;
    expect([left, top, width, height, pageStates(rig)]).toEqual(['10%', '10%', '40%', '20%', ['current', 'idle']]);
    frameAt(rig, 0.1 + 10.5);
    expect([current(rig), pageStates(rig)]).toEqual(['4', ['idle', 'current']]);
    frameAt(rig, 0.1 + 18);
    expect([current(rig), pageStates(rig), rig.host.dataset.positionMs]).toEqual(['-1', ['idle', 'idle'], '18000']);
  });

  it.each([50, 75, 100, 120])('follows the heard position at %i percent speed (A-012-1)', async (speed) => {
    const rig = attached();
    rig.transport.setTiming({ rate: speed / 100, latency: 0 });
    await rig.transport.play();
    const at = (track: number): number => 0.1 + track / (speed / 100);
    frameAt(rig, at(5.9995));
    expect(current(rig)).toBe('1');
    frameAt(rig, at(6.0005));
    expect(current(rig)).toBe('2');
  });

  it('jumps back at the loop wrap and after a seek', async () => {
    const rig = attached();
    rig.transport.setLoop({ start: 4, end: 8, enabled: true });
    await rig.transport.play();
    frameAt(rig, 0.1 + 3.9);
    expect(current(rig)).toBe('2');
    frameAt(rig, 0.1 + 4.1);
    expect(current(rig)).toBe('1');
    rig.transport.setLoop({ start: 0, end: 20, enabled: false });
    rig.transport.seek(15);
    frameAt(rig, rig.ctx.currentTime + 0.2);
    expect(current(rig)).toBe('6');
  });

  it('draws one frame after a pause or stop with the stored position, and stops drawing', async () => {
    const rig = attached();
    await rig.transport.play();
    frameAt(rig, 3.1);
    rig.transport.pause();
    rig.frames.step(3200);
    expect([current(rig), rig.host.dataset.positionMs, rig.frames.size]).toEqual(['0', '3000', 0]);
    rig.transport.stop();
    rig.frames.step(3300);
    expect(current(rig)).toBe('-1');
  });

  it('scrolls once per system change (page or y), smoothly, centred', async () => {
    const rig = attached();
    await rig.transport.play();
    [2.1, 3, 4.2, 6.2, 8.2, 10.2].forEach((t) => {
      frameAt(rig, t);
    });
    expect(rig.scrolls.map((s) => s.page)).toEqual(['1', '1', '2']);
    expect(rig.scrolls[0]?.options).toEqual({ block: 'center', inline: 'nearest', behavior: 'smooth' });
  });

  it('does not scroll with follow off; the highlight is still drawn', async () => {
    const rig = attached({ follow: false, reducedMotion: false });
    await rig.transport.play();
    frameAt(rig, 2.2);
    expect([current(rig), rig.scrolls.length]).toEqual(['0', 0]);
  });

  it('a manual scroll (wheel, touchmove, scroll keys) pauses auto-scroll for scrollPauseMs; Follow resumes', async () => {
    const rig = makeRig();
    rig.follower.attach(HOST, VIEW, { follow: true, reducedMotion: false, scrollPauseMs: 4000 });
    await rig.transport.play();
    frameAt(rig, 2.2);
    rig.host.dispatchEvent(new Event('wheel'));
    frameAt(rig, 6.2);
    expect(rig.scrolls).toHaveLength(1);
    rig.nowMs = 4001;
    frameAt(rig, 10.2);
    expect(rig.scrolls).toHaveLength(2);
    rig.host.dispatchEvent(new KeyboardEvent('keydown', { key: 'PageDown' }));
    rig.host.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }));
    rig.host.dispatchEvent(new Event('touchmove'));
    rig.follower.setOptions({ follow: true });
    frameAt(rig, 10.3);
    expect(rig.scrolls).toHaveLength(3);
  });

  it('with reduced motion it jumps, and only when the bar left the visible part of the host', async () => {
    const rig = attached({ follow: true, reducedMotion: true });
    await rig.transport.play();
    frameAt(rig, 2.2);
    expect(rig.scrolls).toEqual([]);
    rig.visible = false;
    frameAt(rig, 6.2);
    expect(rig.scrolls[0]?.options).toEqual({ block: 'center', inline: 'nearest', behavior: 'auto' });
  });

  it('defaults reducedMotion to prefers-reduced-motion', async () => {
    vi.stubGlobal('matchMedia', (query: string) => ({ matches: query === '(prefers-reduced-motion: reduce)' }));
    const rig = attached({ follow: true } as { follow: boolean; reducedMotion: boolean });
    rig.visible = false;
    await rig.transport.play();
    frameAt(rig, 2.2);
    expect(rig.scrolls[0]?.options?.behavior).toBe('auto');
    vi.unstubAllGlobals();
  });

  it('an invalid view or option keeps the attached follower unchanged', async () => {
    const rig = attached();
    expect(() => {
      rig.follower.attach(HOST, { pages: VIEW.pages, bars: [] }, { follow: true });
    }).toThrow('Invalid score view');
    expect(() => {
      rig.follower.setOptions({ follow: true, scrollPauseMs: -1 });
    }).toThrow('Invalid score follow options');
    await rig.transport.play();
    frameAt(rig, 2.2);
    expect([current(rig), rig.host.querySelectorAll('.score-highlight').length]).toEqual(['0', 2]);
  });

  it('re-attaching replaces the highlights; detach removes them and the host attributes', () => {
    const rig = attached();
    rig.follower.attach(HOST, VIEW, { follow: true });
    expect(rig.host.querySelectorAll('.score-highlight')).toHaveLength(2);
    rig.follower.detach();
    rig.follower.detach();
    expect([rig.host.querySelectorAll('.score-highlight').length, rig.host.dataset.barIndex, rig.host.dataset.positionMs]).toEqual([0, undefined, undefined]);
    rig.host.dispatchEvent(new Event('wheel'));
    rig.follower.setOptions({ follow: false });
  });

  it('costs nothing between bar changes: style writes only on a change (frame cost counter)', async () => {
    const rig = attached();
    await rig.transport.play();
    for (let t = 2.1; t < 3.9; t += 1 / 60) {
      frameAt(rig, t);
    }
    expect(rig.follower.stats.barChanges).toBe(1);
    expect(rig.follower.stats.frames).toBeGreaterThan(100);
  });

  it('redraws after a live rate change; the default visibility check reads the bounding boxes', () => {
    const base = makeTransport();
    makeHost();
    const frames = new FakeFrames();
    const loop = new FrameLoop({ frames, document });
    const follower = new ScoreFollower({ loop, position: () => base.transport.positionAt(base.ctx.currentTime), playing: () => false });
    base.transport.addListener(follower);
    follower.attach(HOST, VIEW, { follow: true, reducedMotion: true });
    frames.step(0);
    follower.onTimingChanged();
    expect(frames.size).toBe(1);
    base.transport.seek(2.5);
    frames.step(16);
    expect(document.getElementById(HOST)?.dataset.barIndex).toBe('0');
  });

  it('a page without a container gets no highlight but the host attributes still follow', async () => {
    const rig = makeRig();
    rig.host.querySelector('[data-score-page="2"]')?.remove();
    rig.follower.attach(HOST, VIEW, { follow: true });
    await rig.transport.play();
    frameAt(rig, 10.2);
    expect([current(rig), rig.host.querySelectorAll('.score-highlight').length, rig.scrolls.length]).toEqual(['4', 1, 0]);
  });
});

describe('engine score follower methods', () => {
  beforeEach(() => {
    vi.stubGlobal('requestAnimationFrame', () => 0);
    vi.stubGlobal('cancelAnimationFrame', () => undefined);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('attach, options and detach through the engine; calls after dispose throw', async () => {
    makeHost();
    const ctx = new MockAudioContext();
    const engine = createEngine(new FakeDotNetRef(), { audioContextFactory: () => ctx.asAudioContext() });
    engine.attachScoreFollower(HOST, VIEW, { follow: true, reducedMotion: true, scrollPauseMs: 1000 });
    expect(document.querySelectorAll('.score-highlight')).toHaveLength(2);
    engine.setScoreFollowOptions({ follow: false });
    expect(() => {
      engine.setScoreFollowOptions({ follow: true, scrollPauseMs: 70000 });
    }).toThrow('Invalid score follow options');
    engine.detachScoreFollower();
    expect(document.querySelectorAll('.score-highlight')).toHaveLength(0);
    await engine.dispose();
    expect(() => {
      engine.detachScoreFollower();
    }).toThrow('disposed');
  });
});
