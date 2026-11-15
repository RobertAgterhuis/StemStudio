import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { REDUCED_MOTION_QUERY } from '../../src/karaoke/motion';
import { HOST_ID, LYRICS, attached, makeKaraoke, part, showAt, wordStates, type KaraokeRig } from '../mocks/karaoke-rig';
import { FakeResizeObserver, layoutShift, rowY, stubLayout, stubResizeObserver, wordX } from '../mocks/layout';

/** `MediaQueryList` double whose `matches` tests flip with {@link FakeMediaQuery.set}. */
class FakeMediaQuery {
  public readonly listeners = new Set<(event: MediaQueryListEvent) => void>();

  public constructor(
    public readonly media: string,
    public matches: boolean,
  ) {}

  public addEventListener(_type: 'change', listener: (event: MediaQueryListEvent) => void): void {
    this.listeners.add(listener);
  }

  public removeEventListener(_type: 'change', listener: (event: MediaQueryListEvent) => void): void {
    this.listeners.delete(listener);
  }

  public set(matches: boolean): void {
    this.matches = matches;
    this.listeners.forEach((listener) => {
      listener({ matches, media: this.media } as MediaQueryListEvent);
    });
  }
}

function stubMatchMedia(matches: boolean): FakeMediaQuery {
  const query = new FakeMediaQuery(REDUCED_MOTION_QUERY, matches);
  vi.stubGlobal('matchMedia', (media: string) => (media === REDUCED_MOTION_QUERY ? query : new FakeMediaQuery(media, false)));
  return query;
}

/** Attaches without `reducedMotion`, so the media query decides. */
function attachedByDefault(): KaraokeRig {
  const rig = makeKaraoke();
  rig.karaoke.attach(HOST_ID, LYRICS, { offsetMs: 0 });
  rig.frames.step(0);
  return rig;
}

function ball(rig: KaraokeRig): HTMLElement {
  return part(rig, '.karaoke-ball');
}

async function mutationsDelivered(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

describe('reduced motion', () => {
  beforeEach(() => {
    stubLayout();
    stubResizeObserver();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('option true: no ball, the current word is highlighted', () => {
    const rig = attached(undefined, { reducedMotion: true });
    showAt(rig, 4450);
    expect(ball(rig).hidden).toBe(true);
    expect(ball(rig).style.transform).toBe('');
    expect(wordStates(rig, 'current')).toEqual(['past', 'current', 'future']);
  });

  it('an explicit option wins over the media query and ignores its changes', () => {
    const query = stubMatchMedia(true);
    const rig = attached(undefined, { reducedMotion: false });
    expect(ball(rig).hidden).toBe(false);
    expect(query.listeners.size).toBe(0);
  });

  it('defaults to prefers-reduced-motion and follows its changes at runtime', () => {
    const query = stubMatchMedia(true);
    const rig = attachedByDefault();
    showAt(rig, 4000);
    expect(ball(rig).hidden).toBe(true);
    expect(ball(rig).style.transform).toBe('');
    query.set(false);
    expect(ball(rig).hidden).toBe(false);
    expect(ball(rig).style.transform).toBe(`translate3d(${String(wordX(0))}px, ${String(rowY('current'))}px, 0px)`);
    expect(rig.frames.size).toBe(1);
    query.set(true);
    expect(ball(rig).hidden).toBe(true);
  });

  it('without matchMedia the ball is shown', () => {
    const rig = attachedByDefault();
    expect(ball(rig).hidden).toBe(false);
  });

  it('detach stops following the media query', () => {
    const query = stubMatchMedia(false);
    const rig = attachedByDefault();
    expect(query.listeners.size).toBe(1);
    rig.karaoke.detach();
    expect(query.listeners.size).toBe(0);
  });
});

describe('text-size change', () => {
  beforeEach(() => {
    stubResizeObserver();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('a class or data-size change on the host re-measures, moves the ball and draws one frame', async () => {
    const reads = stubLayout();
    const rig = attached();
    showAt(rig, 4000);
    reads.mockClear();
    layoutShift.x = 20;
    rig.host.classList.add('karaoke-size-l');
    await mutationsDelivered();
    expect(reads).toHaveBeenCalled();
    expect(ball(rig).style.transform).toBe(`translate3d(${String(wordX(0))}px, ${String(rowY('current'))}px, 0px)`);
    expect(rig.frames.size).toBe(1);
    rig.frames.step(1);
    layoutShift.x = 40;
    rig.host.dataset.size = 's';
    await mutationsDelivered();
    expect(ball(rig).style.transform).toBe(`translate3d(${String(wordX(0))}px, ${String(rowY('current'))}px, 0px)`);
  });

  it('other host attributes (written by the engine or Blazor) do not re-measure', async () => {
    const reads = stubLayout();
    const rig = attached();
    reads.mockClear();
    rig.host.dataset.sync = 'line';
    showAt(rig, 4000);
    await mutationsDelivered();
    expect(reads).not.toHaveBeenCalled();
  });

  it('detach stops watching the host attributes', async () => {
    const reads = stubLayout();
    const rig = attached();
    rig.karaoke.detach();
    reads.mockClear();
    rig.host.classList.add('karaoke-size-s');
    await mutationsDelivered();
    expect(reads).not.toHaveBeenCalled();
    expect(FakeResizeObserver.latest().disconnected).toBe(true);
  });
});
