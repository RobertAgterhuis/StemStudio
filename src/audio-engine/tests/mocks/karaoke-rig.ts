import { FrameLoop } from '../../src/frame-loop';
import { Karaoke } from '../../src/karaoke/index';
import { KaraokeClock } from '../../src/karaoke/clock';
import type { KaraokeLyrics, KaraokeOptions } from '../../src/types';
import { FakeFrames } from './frames';
import { makeTransport, type TransportRig } from './transport-rig';

/** Self-written fixture text (no third-party lyrics). Line 1 -> 2 has a 7 s gap (countdown from 12 s). */
export const LYRICS: KaraokeLyrics = {
  lines: [
    {
      startMs: 4000,
      endMs: 6000,
      words: [
        { text: 'Hold', startMs: 4000, endMs: 4400 },
        { text: 'the', startMs: 4400, endMs: 4600 },
        { text: 'line', startMs: 4600, endMs: 5500 },
      ],
    },
    {
      startMs: 6000,
      endMs: 8000,
      words: [
        { text: 'Keep', startMs: 6000, endMs: 6500 },
        { text: 'on', startMs: 6500, endMs: 6600 },
        { text: 'going', startMs: 6600, endMs: 7800 },
      ],
    },
    {
      startMs: 15000,
      endMs: 17000,
      words: [
        { text: 'After', startMs: 15000, endMs: 15500 },
        { text: 'gap', startMs: 15500, endMs: 16500 },
      ],
    },
  ],
};

/** Host element id used by the rigs. */
export const HOST_ID = 'karaoke-banner';

/** Karaoke controller wired to a real transport, clock and frame loop over fakes. */
export interface KaraokeRig extends TransportRig {
  readonly frames: FakeFrames;
  readonly loop: FrameLoop;
  readonly clock: KaraokeClock;
  readonly karaoke: Karaoke;
  readonly host: HTMLElement;
}

/** Builds a {@link KaraokeRig} with an empty host `#karaoke-banner` in the global document. */
export function makeKaraoke(): KaraokeRig {
  const host = document.createElement('div');
  host.id = HOST_ID;
  document.body.replaceChildren(host);
  const rig = makeTransport();
  const frames = new FakeFrames();
  const loop = new FrameLoop({ frames, document });
  const clock = new KaraokeClock({ context: rig.ctx.asAudioContext(), transport: rig.transport, now: () => 0 });
  const karaoke = new Karaoke({ loop, clock, playing: () => rig.transport.state === 'playing', document });
  rig.transport.addListener(karaoke);
  return { ...rig, frames, loop, clock, karaoke, host };
}

/** Attaches {@link LYRICS} (or `lyrics`) with offset 0 and draws the attach frame. */
export function attached(lyrics: KaraokeLyrics = LYRICS, options: Partial<KaraokeOptions> = {}): KaraokeRig {
  const rig = makeKaraoke();
  rig.karaoke.attach(HOST_ID, lyrics, { offsetMs: 0, reducedMotion: false, ...options });
  rig.frames.step(0);
  return rig;
}

/** While stopped/paused: seeks to `ms` and draws the resulting frame. */
export function showAt(rig: KaraokeRig, ms: number): void {
  rig.transport.seek(ms / 1000);
  rig.frames.step(ms);
}

/** Starts playback from 0 at context time 0 (`when` = 0.1 s). */
export async function play(rig: KaraokeRig): Promise<void> {
  rig.ctx.currentTime = 0;
  await rig.transport.play();
}

/** While playing: moves the context clock so that track time `ms` is heard, then draws a frame. */
export function frameAt(rig: KaraokeRig, ms: number): void {
  rig.ctx.currentTime = 0.1 + ms / 1000;
  rig.frames.step(ms);
}

/** Host attribute shortcut. */
export function attr(rig: KaraokeRig, name: string): string | null {
  return rig.host.getAttribute(name);
}

/** The engine-created element matching `selector` inside the host. */
export function part(rig: KaraokeRig, selector: string): HTMLElement {
  const element = rig.host.querySelector<HTMLElement>(selector);
  if (element === null) {
    throw new Error(`no ${selector} in the banner`);
  }
  return element;
}

/** `data-state` of every word in a slot, in order. */
export function wordStates(rig: KaraokeRig, slot: 'previous' | 'current' | 'next'): string[] {
  const words = rig.host.querySelectorAll<HTMLElement>(`.karaoke-line[data-slot="${slot}"] .karaoke-word`);
  return [...words].map((w) => w.dataset.state ?? '');
}

/** Text of every word in a slot, in order. */
export function wordTexts(rig: KaraokeRig, slot: 'previous' | 'current' | 'next'): string[] {
  const words = rig.host.querySelectorAll<HTMLElement>(`.karaoke-line[data-slot="${slot}"] .karaoke-word`);
  return [...words].map((w) => w.textContent);
}
