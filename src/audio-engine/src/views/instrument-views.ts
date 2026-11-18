/**
 * The instrument views of one engine (contract 2.6.0, feature 016): chord lane, banner chords and the shared chord
 * display state, wired to the transport, the shared frame loop and the timeline clock. The engine delegates its 2.6.0
 * methods here.
 *
 * @packageDocumentation
 */
import { ChordDisplayState } from '../chords/display';
import { ChordLane } from '../chords/lane';
import type { FrameLoop } from '../frame-loop';
import { TimelineClock } from '../timeline/clock';
import type { Transport } from '../transport';
import type { BeatGrid, ChordChartData, ChordLaneOptions } from '../types';

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
}

/** Chord views and their shared display state. */
export class InstrumentViews {
  private readonly display = new ChordDisplayState();
  private readonly lane: ChordLane;

  /** @param deps - Context, transport, frame loop and grid. */
  public constructor(deps: InstrumentViewsDeps) {
    const { context, transport, loop, grid } = deps;
    const clock = new TimelineClock({ context, transport });
    const playing = (): boolean => transport.state === 'playing';
    this.lane = new ChordLane({ loop, clock, playing, grid, display: this.display });
    transport.addListener(this.lane);
  }

  /** {@inheritDoc AudioEngine.attachChordLane} */
  public attachChordLane(elementId: string, chart: ChordChartData, options?: ChordLaneOptions): void {
    this.lane.attach(elementId, chart, options);
  }

  /** {@inheritDoc AudioEngine.detachChordLane} */
  public detachChordLane(): void {
    this.lane.detach();
  }

  /** @param semitones - The applied 012 transpose; a change re-spells every chord view on the next frame. */
  public setTranspose(semitones: number): void {
    this.display.setTranspose(semitones);
  }

  /** Draws one frame of every attached view (loop change). */
  public redraw(): void {
    this.lane.redraw();
  }

  /** Detaches every view. */
  public dispose(): void {
    this.lane.detach();
  }
}
