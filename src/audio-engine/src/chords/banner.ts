/**
 * Chord symbols in the karaoke banner (contract 2.6.0 `setKaraokeChords`): one `.karaoke-chord` element per chord,
 * inserted in the line slot right before the word whose `[start, nextStart)` holds the chord's start; a chord before
 * the first word of a line (or after the previous line ended) is a lead chord (`data-lead="true"`) at the start of
 * the slot. `N.C.` and chords after the last line are not shown. States follow the karaoke clock like the words.
 *
 * @packageDocumentation
 */
import type { Timeline } from '../karaoke/timeline';
import { Cursor, lastAtOrBefore } from '../timeline/cursor';
import type { ChordChartData } from '../types';
import { DomWriter } from '../views/dom-writer';
import { prepareChart, type PreparedChart } from './chart';
import { SpelledChart, type ChordDisplayState } from './display';

/** A chord drawn into the karaoke banner's slots. */
export interface ChordLayer {
  /** Adds the chords of `line` to a freshly filled slot. */
  decorate(slot: HTMLElement, line: number): void;
  /** Updates states, symbols and the host's `data-chord-index` for karaoke time `t` (ms). */
  paint(t: number, host: HTMLElement): void;
  /** Removes every chord element and the host attribute. */
  clear(host: HTMLElement): void;
}

/** Builds the layer of one attached banner from its timeline. */
export type ChordLayerFactory = (timeline: Timeline) => ChordLayer;

/** A chord anchored to a word of a line (`word` -1 = lead chord). */
interface Anchor {
  readonly segment: number;
  readonly word: number;
}

/** A shown chord element. */
interface Shown {
  readonly element: HTMLElement;
  readonly segment: number;
}

/**
 * Validates the chart and returns the layer factory the karaoke controller keeps across `attachKaraoke` calls.
 *
 * @param chart - Chord chart.
 * @param display - Shared chord display state.
 * @returns The factory.
 * @throws Error `Invalid chord chart: <reason>`.
 */
export function bannerChords(chart: ChordChartData, display: ChordDisplayState): ChordLayerFactory {
  const spelled = new SpelledChart(prepareChart(chart), display);
  return (timeline) => new BannerChords(spelled, timeline);
}

function push(anchors: Map<number, Anchor[]>, line: number, anchor: Anchor): void {
  anchors.set(line, [...(anchors.get(line) ?? []), anchor]);
}

function anchorOne(anchors: Map<number, Anchor[]>, timeline: Timeline, segment: number, startMs: number): void {
  const word = timeline.words[lastAtOrBefore(timeline.wordStarts, startMs)];
  if (word === undefined) {
    push(anchors, 0, { segment, word: -1 });
    return;
  }
  const line = timeline.lines[word.line];
  if (line !== undefined && startMs < line.endMs) {
    push(anchors, word.line, { segment, word: word.index });
  } else if (word.line + 1 < timeline.lines.length) {
    push(anchors, word.line + 1, { segment, word: -1 });
  }
}

function anchorsOf(prepared: PreparedChart, timeline: Timeline): Map<number, Anchor[]> {
  const anchors = new Map<number, Anchor[]>();
  prepared.segments.forEach((segment, i) => {
    if (segment.root !== null) {
      anchorOne(anchors, timeline, i, segment.startMs);
    }
  });
  return anchors;
}

/** The chord layer of one attached banner. */
class BannerChords implements ChordLayer {
  private readonly anchors: Map<number, Anchor[]>;
  private readonly cursor: Cursor;
  private readonly shown = new Map<HTMLElement, Shown[]>();
  private readonly writer = new DomWriter();

  public constructor(
    private readonly chart: SpelledChart,
    timeline: Timeline,
  ) {
    this.anchors = anchorsOf(chart.prepared, timeline);
    this.cursor = new Cursor(chart.prepared.starts);
  }

  public decorate(slot: HTMLElement, line: number): void {
    const words = [...slot.querySelectorAll<HTMLElement>(':scope > .karaoke-word')];
    const shown = (this.anchors.get(line) ?? []).map((anchor) => {
      const element = this.element(slot, anchor);
      this.insert(slot, element, words[anchor.word]);
      return { element, segment: anchor.segment };
    });
    this.shown.set(slot, shown);
  }

  public paint(t: number, host: HTMLElement): void {
    const located = this.cursor.index(t);
    const segment = this.chart.prepared.segments[located];
    const current = segment !== undefined && t < segment.endMs ? located : -1;
    this.shown.forEach((list) => {
      list.forEach((chord) => {
        this.paintChord(chord, current, located);
      });
    });
    this.writer.data(host, 'chordIndex', String(current));
  }

  public clear(host: HTMLElement): void {
    this.shown.forEach((list) => {
      list.forEach(({ element }) => {
        element.remove();
      });
    });
    this.shown.clear();
    host.removeAttribute('data-chord-index');
  }

  private element(slot: HTMLElement, anchor: Anchor): HTMLElement {
    const element = slot.ownerDocument.createElement('span');
    element.className = 'karaoke-chord';
    element.dataset.state = 'future';
    if (anchor.word < 0) {
      element.dataset.lead = 'true';
    }
    element.textContent = this.chart.shape(anchor.segment);
    return element;
  }

  /** Lead chords go first in the slot (in order), the others right before their word. */
  private insert(slot: HTMLElement, element: HTMLElement, word: HTMLElement | undefined): void {
    if (word === undefined) {
      const firstWord = slot.querySelector(':scope > .karaoke-word');
      slot.insertBefore(element, firstWord);
    } else {
      word.before(element);
    }
  }

  private paintChord(chord: Shown, current: number, located: number): void {
    const state = chord.segment === current ? 'current' : chord.segment <= located ? 'past' : 'future';
    this.writer.data(chord.element, 'state', state);
    this.writer.text(chord.element, this.chart.shape(chord.segment));
  }
}
