/**
 * One running singing feedback's scorer and pitch view (contract 2.6.0): the view is drawn on the shared frame loop
 * against the heard position while the feedback runs; after stop it keeps its last window.
 *
 * @packageDocumentation
 */
import type { FrameLoop } from '../frame-loop';
import { FrameListener } from '../timeline/frame-listener';
import { TimelineFrames } from '../timeline/frames';
import type { SingingState, SingingSummary } from '../types';
import type { SessionSpec, SingingSession } from './feedback';
import { PitchView } from './pitch-view';
import { UnitScorer } from './scorer';

/** Engine parts a session needs. */
export interface SessionDeps {
  /** The shared frame loop. */
  readonly loop: FrameLoop;
  /** Heard position (ms). */
  readonly clock: { positionMs(): number };
  /** Whether the transport is playing. */
  readonly playing: () => boolean;
  /** Current 012 transpose. */
  readonly transpose: () => number;
}

/** Scorer + pitch view of one feedback; listens to the transport for its frames. */
export class LiveSession extends FrameListener implements SingingSession {
  private readonly scorer: UnitScorer;
  private readonly view: PitchView;
  protected readonly frames: TimelineFrames;

  /**
   * @param spec - Validated request, host, latency and unit callback.
   * @param deps - Frame loop, clock, transport state and transpose.
   */
  public constructor(spec: SessionSpec, deps: SessionDeps) {
    super();
    const { request } = spec;
    this.frames = new TimelineFrames({ loop: deps.loop, playing: deps.playing, draw: () => {
      this.view.draw(deps.clock.positionMs());
    } });
    const requestFrame = (): void => {
      this.frames.redraw();
    };
    this.view = new PitchView({ host: spec.host, reference: spec.reference, units: request.units, latencySource: spec.latency.source, reducedMotion: request.reducedMotion, transpose: deps.transpose, requestFrame });
    this.scorer = new UnitScorer({ reference: spec.reference, units: request.units, toleranceCents: request.toleranceCents, transpose: deps.transpose, onUnit: (score) => {
      this.view.setLastScore(score.score);
      this.frames.redraw();
      spec.onUnit(score);
    } });
    this.frames.start();
  }

  /** {@inheritDoc SingingSession.frame} */
  public frame(songMs: number, cents: number | null): void {
    this.scorer.frame(songMs, cents);
    this.view.addSinger(songMs, cents);
  }

  /** {@inheritDoc SingingSession.setTolerance} */
  public setTolerance(cents: number): void {
    this.scorer.setTolerance(cents);
  }

  /** {@inheritDoc SingingSession.setState} */
  public setState(state: SingingState): void {
    this.view.setState(state);
    this.frames.redraw();
  }

  /** {@inheritDoc SingingSession.reposition} */
  public reposition(): void {
    this.scorer.reposition();
  }

  /** {@inheritDoc SingingSession.summary} */
  public summary(): Omit<SingingSummary, 'latencySource'> {
    return this.scorer.summary();
  }

  /** {@inheritDoc SingingSession.stop} */
  public stop(): void {
    this.frames.stop();
  }

  /** {@inheritDoc SingingSession.dispose} */
  public dispose(): void {
    this.frames.stop();
    this.view.dispose();
  }
}
