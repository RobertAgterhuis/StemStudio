/**
 * Test double of the time-stretch stage (contract 2.3.0 "Testing"): identity pitch, fixed `latencyFrames`, records
 * `setPitch`/`reset` calls. Injected through `EngineOptions.timeStretchFactory`; never used by the shipped engine.
 *
 * @packageDocumentation
 */
import type { TimeStretchStage } from '../types';
import { STRETCH_DELAY_FRAMES } from './constants';
import type { FailureAware } from './worklet-stage';

/** One recorded `setPitch` call. */
export interface RecordedPitch {
  /** Pitch factor. */
  readonly factor: number;
  /** Context frame of the stage input. */
  readonly atFrame: number;
}

/** Identity stage over one gain node. */
export class FakeStretchStage implements TimeStretchStage, FailureAware {
  public readonly input: AudioNode;
  public readonly pitches: RecordedPitch[] = [];
  public onfailure: ((reason: string) => void) | null = null;
  public resets = 0;
  public underrunCount = 0;
  public disposed = false;

  /**
   * @param context - Context creating the pass-through node.
   * @param latencyFrames - Reported fixed delay D.
   */
  public constructor(
    context: BaseAudioContext,
    public readonly latencyFrames = STRETCH_DELAY_FRAMES,
  ) {
    this.input = context.createGain();
  }

  /** Same node as the input (identity). */
  public get output(): AudioNode {
    return this.input;
  }

  /** {@inheritDoc TimeStretchStage.setPitch} */
  public setPitch(pitchFactor: number, atFrame: number): void {
    this.pitches.push({ factor: pitchFactor, atFrame });
  }

  /** {@inheritDoc TimeStretchStage.reset} */
  public reset(): void {
    this.resets += 1;
  }

  /** {@inheritDoc TimeStretchStage.underruns} */
  public underruns(): number {
    return this.underrunCount;
  }

  /** {@inheritDoc TimeStretchStage.dispose} */
  public dispose(): void {
    this.disposed = true;
  }

  /** @param reason - Simulates a processor error after ready. */
  public fail(reason: string): void {
    this.onfailure?.(reason);
  }
}
