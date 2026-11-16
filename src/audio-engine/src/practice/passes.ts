/**
 * Explicit loop passes (contract 2.3.0 "Explicit passes", R-10): pass k is one set of sources started together with
 * `start(T_k - L, A, B - A)` at one rate; the next pass is created by a look-ahead timer well before the previous one
 * ends, so a pass without a gap starts sample-exactly where the previous one stops (source time). The timer only
 * wakes the scheduler; every start time is computed from the previous pass, never from the timer.
 *
 * @packageDocumentation
 */
import { isInside } from '../loop';
import type { PassPlanner, PassSpec, Transport } from '../transport';
import type { LoopRegion } from '../types';
import type { TrainerProgress, TrainerSnapshot } from './trainer';

/** Shortest loop (track seconds) that uses explicit passes. */
export const MIN_PASS_SECONDS = 0.5;
/** The next pass is created when the previous one ends within this many seconds (>= 0.3 s plus a timer period). */
export const PASS_LOOKAHEAD_SECONDS = 0.4;
/** Wake-up interval of the pass scheduler (ms). */
export const PASS_TICK_MS = 25;

/** Rate, heard latency and pitch factor of one pass, with the trainer state it plays. */
export interface PassTempo {
  /** Playback rate. */
  readonly rate: number;
  /** Heard latency L of the stage (0 when bypassed). */
  readonly latency: number;
  /** Pitch factor of the stage for this pass. */
  readonly pitch: number;
  /** Trainer state while this pass is heard. */
  readonly label: TrainerSnapshot;
}

/** The pass after the current one. */
export interface NextPass extends PassTempo {
  /** Leave the explicit passes: an open start with the native loop. */
  readonly open: boolean;
  /** Silence before the pass (count-in gap), context seconds. */
  readonly gap: number;
  /** Trainer progress before this pass was counted (restored when it is never heard). */
  readonly undo: TrainerProgress | null;
}

/** What the scheduler needs from the practice controller. */
export interface PassHost {
  /** The engine's AudioContext. */
  readonly context: BaseAudioContext;
  /** The transport. */
  readonly transport: Transport;
  /** Current loop region. */
  region(): LoopRegion;
  /** Tempo of the first pass of a start (starts the trainer). */
  first(): PassTempo;
  /** Tempo of the next pass (advances the trainer). */
  next(): NextPass;
  /** A pass was created: its pitch applies from source time `sourceStart`; `spec` is what the transport started. */
  scheduled(pitch: number, sourceStart: number, spec: PassSpec): void;
  /** Another pass is heard now. */
  heard(): void;
}

/** One created pass. */
export interface PassRecord {
  /** Source time of its first frame. */
  readonly sourceStart: number;
  /** Source time after its last frame. */
  readonly sourceEnd: number;
  /** Heard time of its first frame. */
  readonly heardWhen: number;
  /** Trainer state while it is heard. */
  readonly label: TrainerSnapshot;
  /** Trainer progress to restore when it is never heard. */
  readonly undo: TrainerProgress | null;
}

/** Plans the first pass of every start and creates the following ones ahead of time. */
export class PassScheduler implements PassPlanner {
  private records: PassRecord[] = [];
  private timer: ReturnType<typeof setInterval> | undefined;
  private heardRecord: PassRecord | undefined;

  /** @param host - Controller hooks. */
  public constructor(private readonly host: PassHost) {}

  /** {@inheritDoc PassPlanner.first} */
  public first(offset: number, heardWhen: number): PassSpec {
    const region = this.host.region();
    const tempo = this.host.first();
    const start = isInside(offset, region) ? offset : region.start;
    const duration = region.end - start;
    const sourceStart = heardWhen - tempo.latency;
    this.records = [{ sourceStart, sourceEnd: sourceStart + duration / tempo.rate, heardWhen, label: tempo.label, undo: null }];
    this.heardRecord = undefined;
    return { heardWhen, offset: start, duration, rate: tempo.rate, latency: tempo.latency };
  }

  /** Starts the wake-up timer (playback started). */
  public start(): void {
    this.stop();
    this.timer = setInterval(this.tick, PASS_TICK_MS);
  }

  /** Stops the wake-up timer. */
  public stop(): void {
    clearInterval(this.timer);
    this.timer = undefined;
  }

  /**
   * @param time - Context time.
   * @returns The pass heard at `time` (the first one before it starts), or undefined without passes.
   */
  public heardAt(time: number): PassRecord | undefined {
    return this.records.filter((record) => record.heardWhen <= time).at(-1) ?? this.records[0];
  }

  /**
   * @param time - Source time.
   * @returns Every pass whose sources start after `time`, plus the one playing at `time` first.
   */
  public fromSourceTime(time: number): PassRecord[] {
    const playing = this.records.filter((record) => record.sourceStart <= time).at(-1);
    return [...(playing === undefined ? [] : [playing]), ...this.records.filter((record) => record.sourceStart > time)];
  }

  /**
   * @param time - Context time (pause).
   * @returns Trainer progress before the first created pass that is not heard yet, else null.
   */
  public unheardUndo(time: number): TrainerProgress | null {
    return this.records.find((record) => record.heardWhen > time)?.undo ?? null;
  }

  private readonly tick = (): void => {
    const now = this.host.context.currentTime;
    const last = this.records.at(-1);
    if (last !== undefined && now >= last.sourceEnd - PASS_LOOKAHEAD_SECONDS) {
      this.create(last);
    }
    this.reportHeard(now);
  };

  private create(last: PassRecord): void {
    const next = this.host.next();
    const sourceStart = last.sourceEnd + next.gap;
    const spec = this.specOf(next, sourceStart);
    this.host.transport.launch(spec);
    this.host.scheduled(next.pitch, sourceStart, spec);
    if (spec.duration === undefined) {
      this.host.transport.setPlanner(null);
      this.records = [];
      this.stop();
      return;
    }
    const sourceEnd = sourceStart + spec.duration / next.rate;
    this.records = [...this.records.slice(-3), { sourceStart, sourceEnd, heardWhen: spec.heardWhen, label: next.label, undo: next.undo }];
  }

  /** The next pass from the loop start (an open start with the native loop when leaving the passes). */
  private specOf(next: NextPass, sourceStart: number): PassSpec {
    const region = this.host.region();
    const spec = { heardWhen: sourceStart + next.latency, offset: region.start, rate: next.rate, latency: next.latency };
    return next.open ? spec : { ...spec, duration: region.end - region.start };
  }

  private reportHeard(now: number): void {
    const record = this.records.filter((candidate) => candidate.heardWhen <= now).at(-1);
    if (record !== this.heardRecord) {
      this.heardRecord = record;
      this.host.heard();
    }
  }
}
