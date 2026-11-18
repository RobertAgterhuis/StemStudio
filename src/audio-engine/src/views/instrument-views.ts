/**
 * The instrument views of one engine (contract 2.6.0, feature 016): chord lane, banner chords and the shared chord
 * display state, wired to the transport, the shared frame loop and the timeline clock. The engine delegates its 2.6.0
 * methods here.
 *
 * @packageDocumentation
 */
import { bannerChords } from '../chords/banner';
import { ChordDisplayState } from '../chords/display';
import { ChordLane } from '../chords/lane';
import { HitLanes } from '../drums/hit-lanes';
import type { FrameLoop } from '../frame-loop';
import type { Karaoke } from '../karaoke/index';
import { TimelineClock } from '../timeline/clock';
import type { Transport } from '../transport';
import type { BeatGrid, ChordChartData, ChordDisplay, ChordLaneOptions, DrumHitEvent, HitLaneOptions } from '../types';

/** Construction parameters of {@link InstrumentViews}. */
export interface InstrumentViewsDeps {
  /** The engine's AudioContext. */
  readonly context: AudioContext;
  /** The transport (the views listen to it). */
  readonly transport: Transport;
  /** The shared frame loop. */
  readonly loop: FrameLoop;
  /** Beat grid of the loaded track. */
  readonly grid: () => BeatGrid | null;
  /** The karaoke banner (chord symbols above its words). */
  readonly karaoke: Karaoke;
}

/** Chord views and their shared display state. */
export class InstrumentViews {
  private readonly display = new ChordDisplayState();
  private readonly lane: ChordLane;
  private readonly karaoke: Karaoke;
  private readonly hitLanes: HitLanes;

  /** @param deps - Context, transport, frame loop, grid and karaoke banner. */
  public constructor(deps: InstrumentViewsDeps) {
    const { context, transport, loop, grid } = deps;
    this.karaoke = deps.karaoke;
    this.display.onChange(() => {
      this.karaoke.redraw();
    });
    const clock = new TimelineClock({ context, transport });
    const playing = (): boolean => transport.state === 'playing';
    this.lane = new ChordLane({ loop, clock, playing, grid, display: this.display });
    this.hitLanes = new HitLanes({ loop, clock, playing, grid });
    transport.addListener(this.lane);
    transport.addListener(this.hitLanes);
  }

  /** {@inheritDoc AudioEngine.attachChordLane} */
  public attachChordLane(elementId: string, chart: ChordChartData, options?: ChordLaneOptions): void {
    this.lane.attach(elementId, chart, options);
  }

  /** {@inheritDoc AudioEngine.detachChordLane} */
  public detachChordLane(): void {
    this.lane.detach();
  }

  /** {@inheritDoc AudioEngine.attachHitLanes} */
  public attachHitLanes(elementId: string, hits: readonly DrumHitEvent[], options?: HitLaneOptions): void {
    this.hitLanes.attach(elementId, hits, options);
  }

  /** {@inheritDoc AudioEngine.setHitLaneOptions} */
  public setHitLaneOptions(options: HitLaneOptions): void {
    this.hitLanes.setOptions(options);
  }

  /** {@inheritDoc AudioEngine.detachHitLanes} */
  public detachHitLanes(): void {
    this.hitLanes.detach();
  }

  /** {@inheritDoc AudioEngine.setKaraokeChords} */
  public setKaraokeChords(chart: ChordChartData | null): void {
    this.karaoke.setChords(chart === null ? null : bannerChords(chart, this.display));
  }

  /** {@inheritDoc AudioEngine.setChordDisplay} */
  public setChordDisplay(display: ChordDisplay): void {
    this.display.set(display);
  }

  /** @param semitones - The applied 012 transpose; a change re-spells every chord view on the next frame. */
  public setTranspose(semitones: number): void {
    this.display.setTranspose(semitones);
  }

  /** Draws one frame of every attached view (loop change). */
  public redraw(): void {
    this.lane.redraw();
    this.hitLanes.redraw();
  }

  /** Detaches every view. */
  public dispose(): void {
    this.lane.detach();
    this.hitLanes.detach();
  }
}
