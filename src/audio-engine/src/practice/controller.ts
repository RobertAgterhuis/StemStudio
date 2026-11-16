/**
 * Practice tempo controller (contract 2.3.0 "Practice tempo", data-model 3.1): validates the settings, loads the
 * time-stretch stage on demand, engages/bypasses it, applies live speed/transpose changes on one shared time and
 * keeps the {@link PracticeStatus} snapshot.
 *
 * @packageDocumentation
 */
import { anchorOf } from '../anchors';
import type { ClickOutput } from '../metronome/clicks';
import type { Mixer } from '../mixer';
import { contentOffsetFrames, isNeutral, needsStretch, pitchFactor, speedRate } from '../stretch/params';
import { UNAVAILABLE_PREFIX, type FailureAware } from '../stretch/worklet-stage';
import type { Transport, TransportListener } from '../transport';
import type { PlaybackState, PracticeSettings, PracticeStatus, StretchState, TimeStretchStage } from '../types';
import { DEFAULT_PRACTICE, validatePractice } from './settings';
import { CountIns, type TempoSource } from './count-ins';
import { PassMode, type Tempo } from './pass-mode';
import { CHANGE_LEAD_SECONDS, liveChangeTimes, type LiveChangeTimes } from './tempo';

/** Interval of the underrun poll while playing (ms); UI diagnostics only. */
export const UNDERRUN_POLL_MS = 100;

/** What the controller needs from the engine. */
export interface PracticeHost {
  /** The engine's AudioContext. */
  readonly context: AudioContext;
  /** The transport. */
  readonly transport: Transport;
  /** The mixer (stage insertion). */
  readonly mixer: Mixer;
  /** Creates the stretch stage (worklet or test fake). */
  readonly factory: (context: AudioContext) => Promise<TimeStretchStage>;
  /** Requests an `OnPracticeChanged` report (throttled by the caller). */
  readonly changed: () => void;
  /** Metronome click output (count-in clicks). */
  readonly clicks: ClickOutput;
  /** Start delay of sample-locked starts (s). */
  readonly startDelay: number;
  /** Beat grid and metronome settings (count-in tempo). */
  readonly tempoSource: () => TempoSource;
}

/** Rate and pitch factor heard for the current settings. */
interface Target {
  readonly rate: number;
  readonly pitch: number;
}

/** Owns the practice settings and the stage. */
export class PracticeController implements TransportListener {
  private settings: PracticeSettings = DEFAULT_PRACTICE;
  private stretch: StretchState = 'bypass';
  private reason: string | null = null;
  private stage: TimeStretchStage | null = null;
  private loading: Promise<void> | null = null;
  private failed: string | null = null;
  private active: TimeStretchStage | null = null;
  private applied = 1;
  private pending: { readonly at: number; readonly before: number } | null = null;
  private underrunBase = 0;
  private reportedUnderruns = 0;
  private poll: ReturnType<typeof setInterval> | undefined;
  private disposed = false;
  private restarts = 0;
  private transpose = 0;
  private readonly passMode: PassMode;
  private readonly countIns: CountIns;

  /** @param host - Engine parts. */
  public constructor(private readonly host: PracticeHost) {
    this.countIns = new CountIns({ ...host, changed: host.changed, tempoSource: host.tempoSource });
    this.passMode = new PassMode({
      context: host.context,
      transport: host.transport,
      settings: () => this.settings,
      tempoFor: (speed) => this.tempoFor(speed),
      stage: () => this.active,
      gap: (rate) => (this.settings.countIn.everyPass ? this.countIns.gap(this.settings.countIn.bars, rate) : 0),
      countIn: (heardWhen, rate) => {
        if (this.settings.countIn.everyPass) {
          this.countIns.beforePass(this.settings.countIn.bars, heardWhen, rate);
        }
      },
      applied: (pitch) => {
        this.applied = pitch;
      },
      changed: host.changed,
    });
  }

  /**
   * Validates and applies settings; loads the stage first when they need it.
   *
   * @param input - Requested settings.
   */
  public async apply(input: PracticeSettings): Promise<void> {
    const settings = validatePractice(input);
    if (needsStretch(settings) && this.stage === null) {
      await this.ensureStage(settings);
    }
    this.settings = settings;
    const restart = this.passMode.configure(settings);
    const restarts = this.restarts;
    this.retarget();
    this.transpose = settings.transposeSemitones;
    if (restart && this.restarts === restarts) {
      this.host.transport.restart();
    }
    this.host.changed();
  }

  /** Starts playback, after a count-in when one is set and a tempo is known (`play()` resolves at once). */
  public async play(): Promise<void> {
    const { transport, context } = this.host;
    if (transport.state === 'playing') {
      return;
    }
    await context.resume();
    const latency = this.engaged && isNeutral(this.settings) ? 0 : transport.timing.latency;
    await transport.play(this.countIns.beforePlay(this.settings.countIn.bars, this.target().rate, latency));
  }

  /** The loop changed (after the transport applied it): passes may start, stop or restart. */
  public onLoopChanged(): void {
    if (this.passMode.loopChanged()) {
      this.host.transport.restart();
    }
    this.host.changed();
  }

  private get engaged(): boolean {
    return this.active !== null;
  }

  /** @returns The current snapshot. */
  public status(): PracticeStatus {
    const { speedPercent, transposeSemitones, countIn } = this.settings;
    const trainer = this.passMode.trainerStatus();
    const runs = trainer.state === 'running' || trainer.state === 'target';
    return {
      speedPercent: runs ? trainer.speedPercent : speedPercent,
      transposeSemitones,
      stretch: this.stretch,
      stretchReason: this.reason,
      latencyMs: this.active === null ? 0 : this.latencyOf(this.active, this.applied) * 1000,
      underruns: this.underruns(),
      trainer,
      countIn: this.countIns.status(countIn.bars),
    };
  }

  /** A new track: one more load attempt is allowed and the underrun count restarts. */
  public onTrackLoaded(): void {
    this.failed = null;
    if (this.stretch === 'unavailable') {
      this.setStretch('bypass', null);
    }
    this.underrunBase = this.stage?.underruns() ?? 0;
    this.reportedUnderruns = 0;
    this.passMode.trackLoaded();
  }

  /** Transport restart hook: bypass at neutral settings, else reset the stage and set the pitch for the new start. */
  public onRestart(): void {
    this.restarts += 1;
    if (!this.engaged) {
      return;
    }
    if (isNeutral(this.settings)) {
      this.bypass();
      return;
    }
    const { pitch } = this.target();
    this.active?.reset();
    this.active?.setPitch(pitch, 0);
    this.applied = pitch;
    this.pending = null;
  }

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    clearInterval(this.poll);
    this.passMode.onState(state);
    if (state !== 'playing') {
      this.countIns.cancel();
    }
    if (state === 'playing') {
      this.poll = setInterval(this.pollUnderruns, UNDERRUN_POLL_MS);
    } else {
      this.active?.reset();
    }
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(): void {
    this.countIns.cancel();
  }

  /** Frees the stage and stops polling. */
  public dispose(): void {
    this.disposed = true;
    clearInterval(this.poll);
    this.passMode.dispose();
    this.countIns.dispose();
    this.stage?.dispose();
  }

  private ensureStage(settings: PracticeSettings): Promise<void> {
    if (this.failed !== null) {
      this.fallback(settings);
      return Promise.reject(new Error(UNAVAILABLE_PREFIX + this.failed));
    }
    this.loading ??= this.load().finally(() => {
      this.loading = null;
    });
    return this.loading.catch((error: unknown) => {
      this.fallback(settings);
      throw error;
    });
  }

  private async load(): Promise<void> {
    this.setStretch('loading', null);
    try {
      const stage = await this.host.factory(this.host.context);
      this.adopt(stage);
    } catch (error: unknown) {
      const reason = reasonOf(error);
      this.failed = reason;
      this.setStretch('unavailable', reason);
      throw new Error(UNAVAILABLE_PREFIX + reason, { cause: error });
    }
  }

  private adopt(stage: TimeStretchStage): void {
    if (this.disposed) {
      stage.dispose();
      return;
    }
    this.stage = stage;
    this.underrunBase = stage.underruns();
    (stage as Partial<FailureAware>).onfailure = (reason) => {
      this.onStageFailure(reason);
    };
    this.setStretch('active', null);
  }

  /** Keeps 100 %, 0 st and the trainer off, applies everything else (count-in). */
  private fallback(settings: PracticeSettings): void {
    this.settings = { ...settings, speedPercent: 100, transposeSemitones: 0, trainer: { ...settings.trainer, enabled: false } };
    this.host.changed();
  }

  private onStageFailure(reason: string): void {
    const stage = this.stage;
    this.stage = null;
    this.failed = reason;
    if (this.engaged) {
      this.bypass();
      this.host.transport.restart();
    }
    stage?.dispose();
    this.setStretch('unavailable', reason);
    this.fallback(this.settings);
  }

  private retarget(): void {
    const target = this.target();
    const { transport } = this.host;
    if (this.active === null) {
      this.engageFor(target);
    } else if (transport.state !== 'playing') {
      transport.setTiming({ rate: target.rate, latency: this.latencyOf(this.active, target.pitch) });
      this.applied = target.pitch;
    } else if (transport.passes) {
      this.passTranspose(this.active);
    } else {
      this.liveChange(this.active, target);
    }
  }

  private engageFor(target: Target): void {
    if (this.stage === null || !needsStretch(this.settings)) {
      return;
    }
    this.host.mixer.setStage(this.stage);
    this.active = this.stage;
    this.setStretch('active', null);
    this.host.transport.setTiming({ rate: target.rate, latency: this.latencyOf(this.stage, target.pitch) });
    this.applied = target.pitch;
    this.host.transport.restart();
  }

  private bypass(): void {
    this.host.mixer.setStage(null);
    this.active = null;
    this.pending = null;
    this.host.transport.setTiming({ rate: 1, latency: 0 });
    this.setStretch(this.stage === null ? this.stretch : 'bypass', this.reason);
  }

  private liveChange(stage: TimeStretchStage, target: Target): void {
    const { transport } = this.host;
    const base = this.changeBase(this.host.context.currentTime);
    if (base.at === null && this.unchanged(target)) {
      return;
    }
    transport.cancelPending();
    const times = this.changeTimes(stage, base, target.pitch);
    stage.setPitch(target.pitch, times.atFrame);
    const anchor = anchorOf(times.heardWhen, transport.positionAt(times.sampleAt), target.rate);
    transport.retime({ rate: target.rate, latency: this.latencyOf(stage, target.pitch), at: times.at, anchor });
    this.pending = { at: times.at, before: base.before };
    this.applied = target.pitch;
  }

  private changeTimes(stage: TimeStretchStage, base: { at: number | null; before: number }, pitch: number): LiveChangeTimes {
    const { context } = this.host;
    return liveChangeTimes({
      now: context.currentTime,
      pendingAt: base.at,
      delayFrames: stage.latencyFrames,
      sampleRate: context.sampleRate,
      fromPitch: base.before,
      toPitch: pitch,
    });
  }

  /** `U` and pitch before a change that has not entered the stage yet (it is replaced), else none and the applied pitch. */
  private changeBase(now: number): { readonly at: number | null; readonly before: number } {
    return this.pending !== null && this.pending.at > now ? this.pending : { at: null, before: this.applied };
  }

  private unchanged(target: Target): boolean {
    return target.rate === this.host.transport.timing.rate && target.pitch === this.applied;
  }

  /** During explicit passes only the transpose applies live (each pass keeps its rate). */
  private passTranspose(stage: TimeStretchStage): void {
    if (this.settings.transposeSemitones !== this.transpose) {
      this.passMode.transposeLive(stage, this.host.context.currentTime + CHANGE_LEAD_SECONDS);
    }
  }

  private target(): Target {
    const speed = this.passMode.speedFor(this.settings.speedPercent);
    return { rate: speedRate(speed), pitch: pitchFactor(speed, this.settings.transposeSemitones) };
  }

  private tempoFor(speedPercent: number): Tempo {
    const pitch = pitchFactor(speedPercent, this.settings.transposeSemitones);
    return { rate: speedRate(speedPercent), latency: this.active === null ? 0 : this.latencyOf(this.active, pitch), pitch };
  }

  private latencyOf(stage: TimeStretchStage, pitch: number): number {
    return (stage.latencyFrames + contentOffsetFrames(pitch)) / this.host.context.sampleRate;
  }

  private underruns(): number {
    return this.stage === null ? 0 : this.stage.underruns() - this.underrunBase;
  }

  private readonly pollUnderruns = (): void => {
    const count = this.underruns();
    if (count !== this.reportedUnderruns) {
      this.reportedUnderruns = count;
      this.host.changed();
    }
  };

  private setStretch(state: StretchState, reason: string | null): void {
    this.stretch = state;
    this.reason = reason;
    this.host.changed();
  }
}

function reasonOf(error: unknown): string {
  const message = error instanceof Error ? error.message : String(error);
  return message.startsWith(UNAVAILABLE_PREFIX) ? message.slice(UNAVAILABLE_PREFIX.length) : message;
}
