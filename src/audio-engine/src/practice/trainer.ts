/**
 * Speed trainer (data-model 3.2, FR-010): pass 1 at `startPercent`; after every `passesPerStep` completed passes the
 * next pass is `min(speed + stepPercent, targetPercent)`. A pure state machine; the pass scheduler drives it.
 *
 * @packageDocumentation
 */
import type { SpeedTrainerSettings, TrainerState } from '../types';
import { DEFAULT_PRACTICE } from './settings';

/** Trainer part of the practice status. */
export interface TrainerSnapshot {
  /** State. */
  readonly state: TrainerState;
  /** Current pass (1-based), 0 before the first pass. */
  readonly pass: number;
  /** Speed of the current (or next) pass in percent. */
  readonly speedPercent: number;
}

/** Saved progress (see {@link SpeedTrainer.save}). */
export interface TrainerProgress {
  /** Pass number. */
  readonly pass: number;
  /** Completed passes since the last step. */
  readonly completed: number;
  /** Speed in percent. */
  readonly speed: number;
}

/** Trainer progress over explicit loop passes. */
export class SpeedTrainer {
  private settings: SpeedTrainerSettings = DEFAULT_PRACTICE.trainer;
  private usable = false;
  private playing = false;
  private progress: TrainerProgress = { pass: 0, completed: 0, speed: DEFAULT_PRACTICE.trainer.startPercent };

  /** Whether the trainer drives the passes (enabled and a usable loop). */
  public get active(): boolean {
    return this.settings.enabled && this.usable;
  }

  /** Speed of the current pass in percent. */
  public get speedPercent(): number {
    return this.progress.speed;
  }

  /** @param settings - Validated trainer settings; any changed field restarts at pass 1. */
  public configure(settings: SpeedTrainerSettings): void {
    const changed = (Object.keys(settings) as (keyof SpeedTrainerSettings)[]).some((key) => settings[key] !== this.settings[key]);
    this.settings = { ...settings };
    if (changed) {
      this.progress = { pass: this.playing ? 1 : 0, completed: 0, speed: settings.startPercent };
    }
  }

  /** @param usable - Whether an enabled loop of at least 0.5 s exists; losing it resets. */
  public setUsable(usable: boolean): void {
    this.usable = usable;
    if (!usable) {
      this.reset();
    }
  }

  /** Playback started (or resumed): pass 1 when none was played yet. */
  public start(): void {
    this.playing = true;
    if (this.progress.pass === 0) {
      this.progress = { ...this.progress, pass: 1 };
    }
  }

  /** Playback paused: pass and speed are kept. */
  public pause(): void {
    this.playing = false;
  }

  /** Back to pass 1 at the start speed (stop, loadTrack, loop start/end change). */
  public reset(): void {
    this.playing = false;
    this.progress = { pass: 0, completed: 0, speed: this.settings.startPercent };
  }

  /** @returns The speed of the next pass after the current one completed. */
  public completePass(): number {
    const completed = this.progress.completed + 1;
    const step = completed % this.settings.passesPerStep === 0;
    const speed = step ? Math.min(this.progress.speed + this.settings.stepPercent, this.settings.targetPercent) : this.progress.speed;
    this.progress = { pass: this.progress.pass + 1, completed: step ? 0 : completed, speed };
    return speed;
  }

  /** @returns The current progress (to undo a step taken for a pass that was never heard). */
  public save(): TrainerProgress {
    return this.progress;
  }

  /** @param progress - Progress returned by {@link SpeedTrainer.save}. */
  public restore(progress: TrainerProgress): void {
    this.progress = progress;
  }

  /** @returns State, pass (1 while waiting for the first pass, data-model 3.2) and speed. */
  public snapshot(): TrainerSnapshot {
    return { state: this.state(), pass: this.active ? Math.max(1, this.progress.pass) : 0, speedPercent: this.progress.speed };
  }

  private state(): TrainerState {
    if (!this.active) {
      return 'off';
    }
    if (!this.playing) {
      return 'waiting';
    }
    return this.progress.speed >= this.settings.targetPercent ? 'target' : 'running';
  }
}
