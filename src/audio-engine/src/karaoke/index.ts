/**
 * Karaoke banner (contract 2.1.0, ADR-015 D5): attaches a {@link KaraokeView} to a host
 * element and draws it on the engine's shared frame loop - continuously while playing, plus
 * one frame after every attach, seek, pause, stop, loop change, offset change and resize.
 *
 * @packageDocumentation
 */
import type { FrameLoop } from '../frame-loop';
import type { TransportListener } from '../transport';
import type { KaraokeLyrics, KaraokeOptions, PlaybackState } from '../types';
import { validateOffset, type KaraokeClock } from './clock';
import { buildTimeline, validateCountdownGap } from './timeline';
import { KaraokeView } from './view';

/** Construction parameters of {@link Karaoke}. */
export interface KaraokeDeps {
  /** The engine's shared frame loop. */
  readonly loop: FrameLoop;
  /** Karaoke clock (holds the user offset). */
  readonly clock: KaraokeClock;
  /** Whether the transport is playing. */
  readonly playing: () => boolean;
  /** Document holding the host; defaults to `globalThis.document`. */
  readonly document?: Document;
}

/** The engine's karaoke controller; listens to the transport. */
export class Karaoke implements TransportListener {
  private view: KaraokeView | null = null;

  /** @param deps - Frame loop, clock, transport state and document. */
  public constructor(private readonly deps: KaraokeDeps) {}

  /**
   * Validates everything first, then replaces an attached banner.
   *
   * @param elementId - Id of the empty host element.
   * @param lyrics - Timed lyrics.
   * @param options - Offset and drawing options.
   * @throws Error for an unknown host, invalid lyrics or options, or an out-of-range offset.
   */
  public attach(elementId: string, lyrics: KaraokeLyrics, options: KaraokeOptions): void {
    const host = (this.deps.document ?? globalThis.document).getElementById(elementId);
    if (host === null) {
      throw new Error(`Karaoke host not found: ${elementId}`);
    }
    const timeline = buildTimeline(lyrics);
    const gapMs = validateCountdownGap(options.countdownGapMs);
    const offsetMs = validateOffset(options.offsetMs);
    this.detach();
    this.deps.clock.setOffset(offsetMs);
    const requestFrame = (): void => {
      this.deps.loop.once(this.draw);
    };
    this.view = new KaraokeView({ host, timeline, gapMs, clock: this.deps.clock, requestFrame });
    this.follow(this.deps.playing());
  }

  /** @param offsetMs - New user offset (-5000..5000 ms); drawn on the next frame. */
  public setOffset(offsetMs: number): void {
    this.deps.clock.setOffset(offsetMs);
    this.redraw();
  }

  /** Draws one frame when a banner is attached (e.g. after a loop change). */
  public redraw(): void {
    if (this.view !== null) {
      this.deps.loop.once(this.draw);
    }
  }

  /** Removes the banner and stops drawing; no-op when none is attached. */
  public detach(): void {
    this.deps.loop.remove(this.draw);
    this.view?.dispose();
    this.view = null;
  }

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    if (this.view !== null) {
      this.follow(state === 'playing');
    }
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(): void {
    this.redraw();
  }

  private follow(playing: boolean): void {
    if (playing) {
      this.deps.loop.add(this.draw);
    } else {
      this.deps.loop.remove(this.draw);
    }
    this.deps.loop.once(this.draw);
  }

  private readonly draw = (): void => {
    this.view?.draw();
  };
}
