/**
 * One attached chord lane (contract 2.6.0 `attachChordLane`): a `.chord-lane-track` child of the host holds the
 * `.chord-lane-item` elements of the visible segments, each placed once by `style.transform` on a bar timeline; the
 * track itself is translated per frame (scrolling) or per page (reduced motion). Per frame the view reads no layout
 * and writes only what changed (counted by the {@link DomWriter}).
 *
 * @packageDocumentation
 */
import { MotionPreference } from '../karaoke/motion';
import type { TimeScale } from '../timeline/bars';
import { Cursor } from '../timeline/cursor';
import { HostSize } from '../timeline/host-size';
import { pageStart } from '../timeline/paging';
import { DomWriter, px } from '../views/dom-writer';
import type { SpelledChart } from './display';

/** Most `.chord-lane-item` elements in the DOM (research R-12). */
export const MAX_LANE_ITEMS = 40;
/** Items kept before the current one when the window holds more than {@link MAX_LANE_ITEMS}. */
const ITEMS_BEHIND_WHEN_CAPPED = 4;

/** Host attributes written by the lane (removed on detach). */
export const LANE_HOST_ATTRIBUTES = ['data-chord-index', 'data-current-symbol', 'data-next-symbol', 'data-position-ms'] as const;

/** Window of the lane in scale units. */
export interface LaneWindow {
  /** Units shown before the current position. */
  readonly behind: number;
  /** Units shown from the current position on. */
  readonly ahead: number;
}

/** Construction parameters of {@link LaneView}. */
export interface LaneViewOptions {
  /** Host element. */
  readonly host: HTMLElement;
  /** Spelled chart. */
  readonly chart: SpelledChart;
  /** Time scale (bars or seconds) and the window in its units. */
  readonly scale: TimeScale;
  readonly window: LaneWindow;
  /** Explicit reduced motion; undefined follows the media query. */
  readonly reducedMotion: boolean | undefined;
  /** Counted DOM writer. */
  readonly writer: DomWriter;
  /** Requests one frame (resize, motion change). */
  readonly requestFrame: () => void;
}

/** Index range of the items in the DOM (inclusive; empty when `last < first`). */
interface Range {
  readonly first: number;
  readonly last: number;
}

/** The drawing side of one `attachChordLane`. */
export class LaneView {
  private readonly track: HTMLElement;
  private readonly size: HostSize;
  private readonly motion: MotionPreference;
  private readonly current: Cursor;
  private readonly windowStart: Cursor;
  private readonly windowEnd: Cursor;
  private readonly items = new Map<number, HTMLElement>();
  private unitPx = 0;
  private placedPx = -1;
  private spelled = -1;
  private located = Number.NaN;
  private dirty = true;

  /** @param options - Host, chart, scale, motion, writer and frame request. */
  public constructor(private readonly options: LaneViewOptions) {
    const doc = options.host.ownerDocument;
    this.track = doc.createElement('div');
    this.track.className = 'chord-lane-track';
    options.host.replaceChildren(this.track);
    const { starts } = options.chart.prepared;
    this.current = new Cursor(starts);
    this.windowStart = new Cursor(starts);
    this.windowEnd = new Cursor(starts);
    this.size = new HostSize(options.host, this.onLayout);
    this.motion = new MotionPreference(options.reducedMotion, this.onLayout);
  }

  /** @param t - Heard track position (ms). */
  public draw(t: number): void {
    this.unitPx = this.size.width / (this.options.window.behind + this.options.window.ahead);
    const start = this.startUnits(t);
    this.options.writer.style(this.track, 'transform', `translateX(${px(-start * this.unitPx)})`);
    const located = this.current.index(t);
    this.syncItems(this.visible(start, located));
    this.paint(located, t);
    this.options.writer.data(this.options.host, 'positionMs', String(t));
  }

  /** Removes the track, the host attributes and the observers. */
  public dispose(): void {
    this.size.dispose();
    this.motion.dispose();
    this.options.host.replaceChildren();
    LANE_HOST_ATTRIBUTES.forEach((name) => {
      this.options.host.removeAttribute(name);
    });
  }

  private readonly onLayout = (): void => {
    this.options.requestFrame();
  };

  /** Left edge of the window in units: scrolling, or the page start with reduced motion. */
  private startUnits(t: number): number {
    const { behind, ahead } = this.options.window;
    const position = this.options.scale.units(t);
    return (this.motion.reduced ? pageStart(position, ahead) : position) - behind;
  }

  private visible(start: number, located: number): Range {
    const { scale, window } = this.options;
    const { segments } = this.options.chart.prepared;
    const startIndex = this.windowStart.index(scale.ms(start));
    const first = Math.max(startIndex, 0) + (segments[startIndex]?.endMs === scale.ms(start) ? 1 : 0);
    const endMs = scale.ms(start + window.behind + window.ahead);
    const endIndex = this.windowEnd.index(endMs);
    const last = segments[endIndex]?.startMs === endMs ? endIndex - 1 : endIndex;
    return capped({ first, last }, located);
  }

  private syncItems(range: Range): void {
    const relayout = this.placedPx !== this.unitPx;
    [...this.items.keys()].filter((i) => i < range.first || i > range.last).forEach((i) => {
      this.removeItem(i);
    });
    const before = Math.min(...this.items.keys());
    for (let i = range.last; i >= range.first; i -= 1) {
      this.ensureItem(i, before, relayout);
    }
    this.placedPx = this.unitPx;
  }

  private removeItem(index: number): void {
    this.items.get(index)?.remove();
    this.items.delete(index);
    this.options.writer.structural();
    this.dirty = true;
  }

  /** Creates a missing item (prepended below the existing ones, else appended) or re-places it after a resize. */
  private ensureItem(index: number, before: number, relayout: boolean): void {
    const existing = this.items.get(index);
    if (existing === undefined) {
      this.addItem(index, before);
    } else if (relayout) {
      this.place(existing, index, this.options.writer);
    }
  }

  private addItem(index: number, before: number): void {
    const item = this.createItem(index);
    if (index < before) {
      this.track.prepend(item);
    } else {
      this.insertAfterLower(item, index);
    }
    this.items.set(index, item);
    this.options.writer.structural();
    this.dirty = true;
  }

  /** Appends a new upper item in index order (items are created from the last to the first index). */
  private insertAfterLower(item: HTMLElement, index: number): void {
    const next = this.items.get(index + 1);
    if (next === undefined) {
      this.track.append(item);
    } else {
      next.before(item);
    }
  }

  private createItem(index: number): HTMLElement {
    const item = this.track.ownerDocument.createElement('div');
    item.className = 'chord-lane-item';
    const segment = this.options.chart.prepared.segments[index];
    item.dataset.startMs = String(segment?.startMs);
    item.dataset.state = 'future';
    item.textContent = this.options.chart.shape(index);
    this.place(item, index, new DomWriter());
    return item;
  }

  private place(item: HTMLElement, index: number, writer: DomWriter): void {
    const { scale } = this.options;
    const segment = this.options.chart.prepared.segments[index];
    const from = scale.units(Number(segment?.startMs));
    writer.style(item, 'transform', `translateX(${px(from * this.unitPx)})`);
    writer.style(item, 'width', px((scale.units(Number(segment?.endMs)) - from) * this.unitPx));
  }

  /** States, re-spelled texts and host attributes; only when the chord or the spelling changed. */
  private paint(located: number, t: number): void {
    const respell = this.spelled !== this.options.chart.version;
    if (located === this.located && !respell && !this.dirty) {
      return;
    }
    this.dirty = false;
    this.located = located;
    this.spelled = this.options.chart.version;
    const current = this.currentIndex(located, t);
    this.items.forEach((item, i) => {
      this.paintItem(item, i, current, respell);
    });
    this.writeHost(current, located);
  }

  private currentIndex(located: number, t: number): number {
    const segment = this.options.chart.prepared.segments[located];
    return segment !== undefined && t < segment.endMs ? located : -1;
  }

  private paintItem(item: HTMLElement, index: number, current: number, respell: boolean): void {
    const { writer, chart } = this.options;
    writer.data(item, 'state', stateOf(index, current, this.located));
    if (respell) {
      writer.text(item, chart.shape(index));
    }
  }

  private writeHost(current: number, located: number): void {
    const { writer, chart, host } = this.options;
    writer.data(host, 'chordIndex', String(current));
    writer.data(host, 'currentSymbol', chart.shape(current));
    writer.data(host, 'nextSymbol', chart.shape(located + 1));
  }
}

function stateOf(index: number, current: number, located: number): string {
  if (index === current) {
    return 'current';
  }
  return index <= located ? 'past' : 'future';
}

/** Keeps at most {@link MAX_LANE_ITEMS} items, a few before the current one. */
function capped(range: Range, located: number): Range {
  if (range.last - range.first + 1 <= MAX_LANE_ITEMS) {
    return range;
  }
  const centre = Math.min(Math.max(located, range.first), range.last);
  const first = Math.max(range.first, centre - ITEMS_BEHIND_WHEN_CAPPED);
  return { first, last: Math.min(range.last, first + MAX_LANE_ITEMS - 1) };
}
