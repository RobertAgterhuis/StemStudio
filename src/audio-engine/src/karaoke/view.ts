/**
 * One attached banner: DOM, cursors and geometry, drawn by {@link KaraokeView.draw} on an
 * animation frame. Per frame it reads no layout and writes only the ball transform, changed
 * word states and changed host attributes.
 *
 * @packageDocumentation
 */
import { ballAt, ballTransform, hopHeight, hopProgress, type Point } from './ball';
import { Cursor } from '../timeline/cursor';
import { buildBanner, clearBanner, fillSlot, writeData, type BannerDom, type WordState } from './dom';
import { LayoutWatcher, NO_GEOMETRY, measureSlots, type Geometry } from './layout';
import { MotionPreference } from './motion';
import { phaseAt, type Timeline } from './timeline';

/** Construction parameters of {@link KaraokeView}. */
export interface KaraokeViewOptions {
  /** Empty host element. */
  readonly host: HTMLElement;
  /** Validated document. */
  readonly timeline: Timeline;
  /** Gaps longer than this show the countdown (ms). */
  readonly gapMs: number;
  /** Karaoke clock. */
  readonly clock: { positionMs(): number };
  /** Requests one animation frame that calls {@link KaraokeView.draw}. */
  readonly requestFrame: () => void;
  /** Explicit reduced-motion option; undefined follows `prefers-reduced-motion`. */
  readonly reducedMotion?: boolean | undefined;
}

/** Located time: karaoke ms, current line (-1 before the first) and last started word (-1). */
interface Frame {
  readonly t: number;
  readonly line: number;
  readonly word: number;
}

/** The drawing side of one `attachKaraoke`. */
export class KaraokeView {
  private readonly dom: BannerDom;
  private readonly lines: Cursor;
  private readonly words: Cursor;
  private readonly watcher: LayoutWatcher;
  private readonly motion: MotionPreference;
  private geometry: Geometry = NO_GEOMETRY;
  private base = Number.NaN;
  private currentWords: readonly HTMLElement[] = [];
  private last: Frame = { t: 0, line: -1, word: -1 };

  /** @param options - Host, timeline, countdown gap, clock and frame request. */
  public constructor(private readonly options: KaraokeViewOptions) {
    this.dom = buildBanner(options.host);
    this.lines = new Cursor(options.timeline.lineStarts);
    this.words = new Cursor(options.timeline.wordStarts);
    this.watcher = new LayoutWatcher(options.host, this.onResize);
    this.motion = new MotionPreference(options.reducedMotion, this.onMotion);
    this.dom.ball.hidden = this.motion.reduced;
    this.showSlots(Math.max(this.locate(options.clock.positionMs()).line, 0));
    this.measure();
  }

  /** Draws the banner for the current clock position (frame callback). */
  public draw(): void {
    const frame = this.locate(this.options.clock.positionMs());
    this.last = frame;
    this.showSlots(Math.max(frame.line, 0));
    this.paintWords(frame);
    this.writeHost(frame);
    this.placeBall(frame);
  }

  /** Removes the banner DOM and stops observing the layout. */
  public dispose(): void {
    this.watcher.dispose();
    this.motion.dispose();
    clearBanner(this.options.host);
  }

  private readonly onResize = (): void => {
    this.measure();
    this.placeBall(this.last);
    this.options.requestFrame();
  };

  private readonly onMotion = (reduced: boolean): void => {
    this.dom.ball.hidden = reduced;
    this.placeBall(this.last);
    this.options.requestFrame();
  };

  private locate(t: number): Frame {
    return { t, line: this.lines.index(t), word: this.words.index(t) };
  }

  private showSlots(base: number): void {
    if (base === this.base) {
      return;
    }
    this.base = base;
    this.geometry = NO_GEOMETRY;
    this.watcher.watch(this.fillSlots(base));
    if (!this.watcher.active) {
      this.measure();
    }
  }

  /** Fills previous (past), current and next (future) with lines base-1..base+1; returns all words. */
  private fillSlots(base: number): HTMLElement[] {
    const [previous, current, next] = this.dom.slots;
    const { lines } = this.options.timeline;
    const before = fillSlot(previous, lines[base - 1], 'past');
    this.currentWords = fillSlot(current, lines[base], 'future');
    return [...before, ...this.currentWords, ...fillSlot(next, lines[base + 1], 'future')];
  }

  private measure(): void {
    const [previous, current, next] = this.dom.slots;
    const base = this.base;
    this.geometry = measureSlots(this.options.host, [
      { line: base - 1, element: previous },
      { line: base, element: current },
      { line: base + 1, element: next },
    ]);
  }

  private paintWords(frame: Frame): void {
    const first = this.options.timeline.firstWord[this.base] ?? 0;
    const ended = this.lineEnded(frame);
    this.currentWords.forEach((word, i) => {
      writeData(word, 'state', this.wordState(first + i, frame, ended));
    });
  }

  private wordState(global: number, frame: Frame, ended: boolean): WordState {
    if (frame.line < this.base) {
      return 'future';
    }
    if (ended || global < frame.word) {
      return 'past';
    }
    return global === frame.word ? 'current' : 'future';
  }

  private lineEnded(frame: Frame): boolean {
    const line = this.options.timeline.lines[frame.line];
    return line !== undefined && frame.t >= line.endMs;
  }

  private writeHost(frame: Frame): void {
    const { host, countdown } = this.dom;
    const phase = phaseAt(this.options.timeline, frame.line, frame.t, this.options.gapMs);
    writeData(host, 'state', phase.state);
    writeData(host, 'lineIndex', String(frame.line));
    writeData(host, 'wordIndex', String(this.wordIndex(frame)));
    writeData(host, 'positionMs', String(frame.t));
    writeData(countdown, 'count', String(phase.dots));
  }

  private wordIndex(frame: Frame): number {
    const word = this.options.timeline.words[frame.word];
    return word?.line === frame.line && !this.lineEnded(frame) ? word.index : -1;
  }

  /** No ball with reduced motion (the current word's `data-state` is the highlight). */
  private placeBall(frame: Frame): void {
    if (this.motion.reduced) {
      return;
    }
    const point = this.ballPoint(frame);
    const transform = point === null ? '' : ballTransform(point);
    if (transform !== '' && this.dom.ball.style.transform !== transform) {
      this.dom.ball.style.transform = transform;
    }
  }

  /** Resting on the first word before the lyrics and on the last word after its start; else hopping. */
  private ballPoint(frame: Frame): Point | null {
    const next = frame.word + 1;
    if (frame.word < 0 || next >= this.options.timeline.words.length) {
      return this.point(Math.max(frame.word, 0));
    }
    return this.hop(frame, next);
  }

  private hop(frame: Frame, next: number): Point | null {
    const from = this.point(frame.word);
    const to = this.point(next);
    if (from === null || to === null) {
      return null;
    }
    const start = Number(this.options.timeline.wordStarts[frame.word]);
    const end = Number(this.options.timeline.wordStarts[next]);
    return ballAt(from, to, hopProgress(frame.t, start, end), hopHeight(this.geometry.lineHeight, end - start));
  }

  private point(index: number): Point | null {
    const word = this.options.timeline.words[index];
    if (word === undefined) {
      return null;
    }
    return this.geometry.lines.get(word.line)?.[word.index] ?? null;
  }
}
