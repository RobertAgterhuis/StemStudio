/**
 * A scheduled count-in: its clicks are started at exact context times through the metronome output; a wake-up timer
 * only updates the visual beat indicator (`OnPracticeChanged` per beat), never audible timing. Each number is shown for
 * its beat interval, so "1" stays visible until the song's first beat `b` even when its click coincides with the music
 * start (a grid without the song's first beat, `b - A = d`).
 *
 * @packageDocumentation
 */
import type { ClickOutput } from '../metronome/clicks';
import type { CountInClick } from './count-in';

/** Wake-up interval of the beat indicator (ms). */
export const COUNT_IN_TICK_MS = 25;

/** Count-in part of the practice status. */
export interface CountInProgress {
  /** A count-in runs now. */
  readonly active: boolean;
  /** Beats left N..1 while active, 0 otherwise. */
  readonly beatsRemaining: number;
}

interface ScheduledClick {
  readonly node: AudioScheduledSourceNode;
  readonly time: number;
}

/** The current count-in (at most one). */
export class CountInRun {
  private clicks: ScheduledClick[] = [];
  private end = -Infinity;
  private timer: ReturnType<typeof setInterval> | undefined;
  private reported: CountInProgress = { active: false, beatsRemaining: 0 };

  /**
   * @param context - The engine's AudioContext (clock).
   * @param output - Metronome click output (its volume, straight to the destination).
   * @param changed - Requests a status report.
   */
  public constructor(
    private readonly context: BaseAudioContext,
    private readonly output: ClickOutput,
    private readonly changed: () => void,
  ) {}

  /**
   * @param plan - Planned clicks.
   * @param musicStart - Heard context time of the music after them.
   * @param beat - Context seconds of one counted beat (`d / r`).
   */
  public start(plan: readonly CountInClick[], musicStart: number, beat: number): void {
    this.clicks = plan.map((click) => ({ node: this.output.play(click.time, click.accent), time: click.time }));
    this.end = Math.max(musicStart, ...plan.map((click) => click.time + beat));
    clearInterval(this.timer);
    this.timer = setInterval(this.tick, COUNT_IN_TICK_MS);
    this.tick();
  }

  /** Stops every future click (pause, stop, seek, loop change). */
  public cancel(): void {
    const now = this.context.currentTime;
    this.clicks.filter((click) => click.time >= now).forEach((click) => {
      click.node.stop();
    });
    this.clicks = [];
    this.end = -Infinity;
    this.tick();
  }

  /** @returns Whether a count-in runs and how many beats are left. */
  public progress(): CountInProgress {
    const now = this.context.currentTime;
    if (this.clicks.length === 0 || now >= this.end) {
      return { active: false, beatsRemaining: 0 };
    }
    const future = this.clicks.filter((click) => click.time > now).length;
    return { active: true, beatsRemaining: Math.min(this.clicks.length, future + 1) };
  }

  /** Stops the indicator timer. */
  public dispose(): void {
    clearInterval(this.timer);
    this.timer = undefined;
  }

  private readonly tick = (): void => {
    const progress = this.progress();
    if (progress.active !== this.reported.active || progress.beatsRemaining !== this.reported.beatsRemaining) {
      this.reported = progress;
      this.changed();
    }
    if (!progress.active) {
      this.dispose();
    }
  };
}
