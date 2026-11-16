/**
 * Count-in orchestration (contract 2.3.0 "Count-in"): before `play()` and, with `everyPass`, before every loop pass.
 *
 * @packageDocumentation
 */
import type { ClickOutput } from '../metronome/clicks';
import type { Transport } from '../transport';
import type { BeatGrid, MetronomeSettings, PracticeStatus } from '../types';
import { countInLead, countInPlan, countInTempo, type CountInTempo } from './count-in';
import { CountInRun } from './count-in-run';

/** Tempo sources of the count-in. */
export interface TempoSource {
  /** Beat grid of the loaded track. */
  readonly grid: BeatGrid | null;
  /** Current metronome settings. */
  readonly metronome: MetronomeSettings;
}

/** What the count-in needs from the engine. */
export interface CountInHost {
  /** The engine's AudioContext. */
  readonly context: AudioContext;
  /** The transport. */
  readonly transport: Transport;
  /** Metronome click output. */
  readonly clicks: ClickOutput;
  /** Start delay of sample-locked starts (s). */
  readonly startDelay: number;
  /** Grid and metronome settings. */
  tempoSource(): TempoSource;
  /** Requests a status report. */
  changed(): void;
}

/** Plans and runs count-ins. */
export class CountIns {
  private readonly run: CountInRun;

  /** @param host - Engine parts. */
  public constructor(private readonly host: CountInHost) {
    this.run = new CountInRun(host.context, host.clicks, () => {
      host.changed();
    });
  }

  /**
   * @param bars - Counted bars.
   * @returns The count-in part of the status.
   */
  public status(bars: number): PracticeStatus['countIn'] {
    const { metronome } = this.host.tempoSource();
    return { available: this.tempoAt(this.host.transport.startPosition()) !== null, ...this.run.progress(), beatsTotal: bars * metronome.beatsPerBar };
  }

  /**
   * Schedules the count-in of a `play()` from the stored position.
   *
   * @param bars - Counted bars.
   * @param rate - Rate of the start.
   * @param latency - Heard latency of the start.
   * @returns Heard music start, or undefined without a count-in.
   */
  public beforePlay(bars: number, rate: number, latency: number): number | undefined {
    const start = this.host.transport.startPosition();
    const tempo = this.tempoAt(start);
    if (bars === 0 || tempo === null) {
      return undefined;
    }
    const lead = countInLead(tempo, start, rate, bars * this.beatsPerBar());
    const musicStart = this.host.context.currentTime + this.host.startDelay + Math.max(latency, lead);
    this.schedule({ tempo, start, rate, musicStart, bars });
    return musicStart;
  }

  /**
   * @param bars - Counted bars.
   * @param rate - Rate of the next pass.
   * @returns Gap before a pass starting at the loop start (context seconds), 0 without a tempo.
   */
  public gap(bars: number, rate: number): number {
    const start = this.host.transport.loop.start;
    const tempo = this.tempoAt(start);
    return tempo === null ? 0 : countInLead(tempo, start, rate, bars * this.beatsPerBar());
  }

  /**
   * @param bars - Counted bars.
   * @param musicStart - Heard start of the pass.
   * @param rate - Rate of the pass.
   */
  public beforePass(bars: number, musicStart: number, rate: number): void {
    const start = this.host.transport.loop.start;
    const tempo = this.tempoAt(start);
    if (tempo !== null) {
      this.schedule({ tempo, start, rate, musicStart, bars });
    }
  }

  /** Cancels a running count-in. */
  public cancel(): void {
    this.run.cancel();
  }

  /** Stops the indicator timer. */
  public dispose(): void {
    this.run.dispose();
  }

  private schedule(plan: { tempo: CountInTempo; start: number; rate: number; musicStart: number; bars: number }): void {
    this.run.start(countInPlan({ ...plan, beatsPerBar: this.beatsPerBar() }), plan.musicStart);
  }

  private tempoAt(start: number): CountInTempo | null {
    const { grid, metronome } = this.host.tempoSource();
    return countInTempo(grid, metronome, start);
  }

  private beatsPerBar(): number {
    return this.host.tempoSource().metronome.beatsPerBar;
  }
}
