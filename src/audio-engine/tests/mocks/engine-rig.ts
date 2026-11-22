import { vi } from 'vitest';
import { createEngine, type AudioEngine, type BeatGrid, type EngineOptions } from '../../src/index';
import { MockAudioContext } from './audio-context';
import { FakeDotNetRef } from './dotnet-ref';
import { FetchStub } from './fetch';
import { stubResizeObserver } from './layout';
import { hostSize } from './views-rig';

/** A whole engine over the mock context with a 60 s `vocals` stem and stubbed animation frames. */
export interface EngineRig {
  readonly ctx: MockAudioContext;
  readonly ref: FakeDotNetRef;
  readonly stub: FetchStub;
  readonly engine: AudioEngine;
  /** Runs every requested animation frame once. */
  step(): void;
}

/**
 * Builds an {@link EngineRig}; stubs `requestAnimationFrame`, `ResizeObserver` and the layout (host 800 x 120 px).
 *
 * @param options - Extra engine options.
 */
export function makeEngineRig(options: EngineOptions = {}): EngineRig {
  const frames: FrameRequestCallback[] = [];
  vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => frames.push(cb));
  vi.stubGlobal('cancelAnimationFrame', vi.fn());
  stubHostLayout();
  const ctx = new MockAudioContext();
  ctx.decodeDurations.set(100, 60);
  const ref = new FakeDotNetRef();
  const stub = new FetchStub().reply('/vocals', { bytes: 100 });
  const engine = createEngine(ref, { audioContextFactory: () => ctx.asAudioContext(), fetch: stub.fetch, ...options });
  const step = (): void => {
    frames.splice(0).forEach((cb) => {
      cb(0);
    });
  };
  return { ctx, ref, stub, engine, step };
}

/** Stubs `ResizeObserver` and gives every element the 800 x 120 px host rectangle. */
export function stubHostLayout(): void {
  stubResizeObserver();
  hostSize.width = 800;
  hostSize.height = 120;
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(
    () => ({ left: 0, top: 0, width: hostSize.width, height: hostSize.height, right: hostSize.width, bottom: hostSize.height, x: 0, y: 0 }) as DOMRect,
  );
}

/** Loads the 60 s `vocals` stem with `grid`. */
export async function loadVocals(rig: EngineRig, grid: BeatGrid | null = null): Promise<void> {
  await rig.engine.loadTrack({ stems: [{ name: 'vocals', url: '/vocals' }], beatGrid: grid });
}
