/**
 * Glue between the practice settings, the speed trainer and the pass scheduler (contract 2.3.0 "Explicit passes",
 * "Speed trainer"): decides when explicit passes are used, what each pass plays and what the status reports.
 *
 * @packageDocumentation
 */
import { NO_LOOP } from '../loop';
import { pitchFactor } from '../stretch/params';
import type { PassSpec, Transport } from '../transport';
import type { LoopRegion, PlaybackState, PracticeSettings, TimeStretchStage } from '../types';
import { MIN_PASS_SECONDS, PassScheduler, type NextPass, type PassHost, type PassTempo } from './passes';
import { SpeedTrainer, type TrainerSnapshot } from './trainer';

/** Rate, latency and pitch for a speed. */
export interface Tempo {
  /** Playback rate. */
  readonly rate: number;
  /** Heard latency (0 when bypassed). */
  readonly latency: number;
  /** Pitch factor. */
  readonly pitch: number;
}

/** What the pass mode needs from the controller. */
export interface PassModeHost {
  /** The engine's AudioContext. */
  readonly context: BaseAudioContext;
  /** The transport. */
  readonly transport: Transport;
  /** Current (validated) settings. */
  settings(): PracticeSettings;
  /** Tempo of a speed with the current transpose and stage. */
  tempoFor(speedPercent: number): Tempo;
  /** The engaged stage, or null. */
  stage(): TimeStretchStage | null;
  /** Silence before a pass at `rate` (count-in every pass), context seconds. */
  gap(rate: number): number;
  /** A pass heard from `heardWhen` at `rate` was created (count-in clicks before it, when set). */
  countIn(heardWhen: number, rate: number): void;
  /** A pitch factor was sent to the stage for a pass. */
  applied(pitch: number): void;
  /** Requests a status report. */
  changed(): void;
}

/** Explicit passes and the trainer that drives them. */
export class PassMode implements PassHost {
  private readonly trainer = new SpeedTrainer();
  private readonly scheduler = new PassScheduler(this);
  private loop: LoopRegion = NO_LOOP;

  /** @param host - Controller hooks. */
  public constructor(private readonly host: PassModeHost) {}

  /** {@inheritDoc PassHost.context} */
  public get context(): BaseAudioContext {
    return this.host.context;
  }

  /** {@inheritDoc PassHost.transport} */
  public get transport(): Transport {
    return this.host.transport;
  }

  /**
   * @param settings - New settings.
   * @returns Whether playback must restart (passes entered, or the trainer restarted while it ran).
   */
  public configure(settings: PracticeSettings): boolean {
    const before = this.trainer.save();
    this.trainer.configure(settings.trainer);
    const restarted = this.trainer.active && this.trainer.save() !== before;
    return this.updateMode() || (restarted && this.transport.passes && this.playing());
  }

  /** @returns Whether playback must restart after a loop change. */
  public loopChanged(): boolean {
    if (this.loopMoved()) {
      this.trainer.reset();
    }
    this.trainer.setUsable(this.loopUsable());
    const was = this.transport.passes;
    this.transport.setPlanner(this.conditions() ? this.scheduler : null);
    return this.playing() && (was || this.transport.passes);
  }

  /** @param state - New transport state (timer, trainer pause/reset). */
  public onState(state: PlaybackState): void {
    this.scheduler.stop();
    if (state === 'playing') {
      this.scheduler.start();
      return;
    }
    this.undoUnheard();
    if (state === 'stopped') {
      this.trainer.reset();
    } else {
      this.trainer.pause();
    }
  }

  /** A pass created ahead but never heard does not count. */
  private undoUnheard(): void {
    const undo = this.scheduler.unheardUndo(this.context.currentTime);
    if (undo !== null) {
      this.trainer.restore(undo);
    }
  }

  /** Stops the wake-up timer (engine disposal). */
  public dispose(): void {
    this.scheduler.stop();
  }

  /** A new track: the loop is cleared, the trainer restarts. */
  public trackLoaded(): void {
    this.trainer.reset();
    this.loopChanged();
  }

  /**
   * @param speedPercent - Speed of the settings.
   * @returns The speed to play now: the trainer's while it drives the passes, else the settings'.
   */
  public speedFor(speedPercent: number): number {
    return this.trainer.active ? this.trainer.speedPercent : speedPercent;
  }

  /** @returns Trainer state for the status (the pass heard now while passes play). */
  public trainerStatus(): TrainerSnapshot {
    const heard = this.transport.passes && this.playing() ? this.scheduler.heardAt(this.context.currentTime) : undefined;
    return heard !== undefined && this.trainer.active ? heard.label : this.trainer.snapshot();
  }

  /**
   * Applies the transpose live during passes: pitch only (each pass keeps its rate), re-sending the pitch of a pass
   * that is already created.
   *
   * @param stage - The engaged stage.
   * @param at - Source time `U` of the change.
   */
  public transposeLive(stage: TimeStretchStage, at: number): void {
    const st = this.host.settings().transposeSemitones;
    this.scheduler.fromSourceTime(at).forEach((record, i) => {
      const pitch = pitchFactor(record.label.speedPercent, st);
      stage.setPitch(pitch, Math.round((i === 0 ? at : record.sourceStart) * this.context.sampleRate));
      this.host.applied(pitch);
    });
  }

  /** {@inheritDoc PassHost.region} */
  public region(): LoopRegion {
    return this.transport.loop;
  }

  /** {@inheritDoc PassHost.first} */
  public first(): PassTempo {
    if (this.trainer.active) {
      this.trainer.start();
    }
    return this.passTempo(this.host.settings().speedPercent);
  }

  /** {@inheritDoc PassHost.next} */
  public next(): NextPass {
    const settings = this.host.settings();
    if (!this.conditions()) {
      return { ...this.passTempo(settings.speedPercent), open: true, gap: 0, undo: null };
    }
    const undo = this.trainer.active ? this.trainer.save() : null;
    if (this.trainer.active) {
      this.trainer.completePass();
    }
    const tempo = this.passTempo(settings.speedPercent);
    return { ...tempo, open: false, gap: this.host.gap(tempo.rate), undo };
  }

  /** {@inheritDoc PassHost.scheduled} */
  public scheduled(pitch: number, sourceStart: number, spec: PassSpec): void {
    this.host.stage()?.setPitch(pitch, Math.round(sourceStart * this.context.sampleRate));
    this.host.applied(pitch);
    if (spec.duration !== undefined) {
      this.host.countIn(spec.heardWhen, spec.rate);
    }
  }

  /** {@inheritDoc PassHost.heard} */
  public heard(): void {
    this.host.changed();
  }

  private passTempo(speedPercent: number): PassTempo {
    const speed = this.speedFor(speedPercent);
    return { ...this.host.tempoFor(speed), label: { ...this.trainer.snapshot(), speedPercent: speed } };
  }

  /** Sets or clears the planner for the current settings; true when passes start while playing. */
  private updateMode(): boolean {
    const want = this.conditions();
    if (want && !this.transport.passes) {
      this.transport.setPlanner(this.scheduler);
      return this.playing();
    }
    if (!want && !this.playing()) {
      this.transport.setPlanner(null);
    }
    return false;
  }

  /** Remembers the loop; true when its start or end changed. */
  private loopMoved(): boolean {
    const region = this.transport.loop;
    const moved = region.start !== this.loop.start || region.end !== this.loop.end;
    this.loop = region;
    return moved;
  }

  private conditions(): boolean {
    const { trainer, countIn } = this.host.settings();
    return this.loopUsable() && (trainer.enabled || (countIn.everyPass && countIn.bars > 0));
  }

  private loopUsable(): boolean {
    const { loop } = this.transport;
    return loop.enabled && loop.end - loop.start >= MIN_PASS_SECONDS;
  }

  private playing(): boolean {
    return this.transport.state === 'playing';
  }
}
