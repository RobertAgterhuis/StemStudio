/**
 * Display state shared by every chord view (contract 2.6.0 `setChordDisplay`, data-model 2.5 and 2.9): the capo, the
 * "show sounding" switch and the current 012 transpose. Every change bumps a version; the views re-spell on their
 * next frame.
 *
 * @packageDocumentation
 */
import type { ChordDisplay } from '../types';
import type { DomWriter } from '../views/dom-writer';
import type { PreparedChart } from './chart';
import { chordSymbol, type DisplayShift } from './spelling';

/** Class of the sounding-symbol child (contract 2.6.0 `setChordDisplay`). */
export const SOUNDING_CLASS = 'chord-sounding';

/** Highest capo fret. */
export const MAX_CAPO = 12;

/** Capo, sounding switch and transpose of the chord views. */
export class ChordDisplayState {
  private capoFret = 0;
  private sounding = false;
  private semitones = 0;
  private revision = 0;
  private readonly listeners = new Set<() => void>();

  /** Capo fret 0..12. */
  public get capo(): number {
    return this.capoFret;
  }

  /** Whether the sounding symbol is shown next to the shape. */
  public get showSounding(): boolean {
    return this.sounding;
  }

  /** Current 012 transpose (semitones). */
  public get transpose(): number {
    return this.semitones;
  }

  /** Incremented on every change. */
  public get version(): number {
    return this.revision;
  }

  /** The shift of the shapes (transpose and capo). */
  public get shift(): DisplayShift {
    return { transpose: this.semitones, capo: this.capoFret };
  }

  /**
   * @param display - Capo and sounding switch.
   * @throws Error `Capo out of range: <value>` for anything but an integer 0..12 (nothing changes).
   */
  public set(display: ChordDisplay): void {
    if (!Number.isInteger(display.capo) || display.capo < 0 || display.capo > MAX_CAPO) {
      throw new Error(`Capo out of range: ${String(display.capo)}`);
    }
    this.capoFret = display.capo;
    this.sounding = display.showSounding === true;
    this.changed();
  }

  /** @param semitones - The 012 transpose; a change re-spells every view. */
  public setTranspose(semitones: number): void {
    if (semitones !== this.semitones) {
      this.semitones = semitones;
      this.changed();
    }
  }

  /** @param listener - Called after every change (the views request a frame). */
  public onChange(listener: () => void): void {
    this.listeners.add(listener);
  }

  private changed(): void {
    this.revision += 1;
    this.listeners.forEach((listener) => {
      listener();
    });
  }
}

/** Shape and sounding symbols of a chart, re-spelled when the display version changes. */
export class SpelledChart {
  private spelledVersion = -1;
  private shapes: readonly string[] = [];
  private soundings: readonly string[] = [];

  /**
   * @param prepared - The validated chart.
   * @param display - The shared display state.
   */
  public constructor(
    public readonly prepared: PreparedChart,
    private readonly display: ChordDisplayState,
  ) {}

  /** Current 012 transpose of the display (semitones). */
  public get transpose(): number {
    return this.display.transpose;
  }

  /** Capo fret of the display. */
  public get capo(): number {
    return this.display.capo;
  }

  /**
   * Writes the shape of segment `index` into `element`, followed by a `.chord-sounding` child with the sounding symbol
   * when the display shows it. Nothing is written when the element already shows both.
   *
   * @param writer - Counted writer.
   * @param element - Lane item or banner chord.
   * @param index - Segment index.
   */
  public write(writer: DomWriter, element: HTMLElement, index: number): void {
    const shape = this.shape(index);
    if (!this.display.showSounding) {
      writer.text(element, shape);
      return;
    }
    const sounding = this.soundingSymbol(index);
    if (!shows(element, shape, sounding)) {
      element.replaceChildren(shape, soundingChild(element.ownerDocument, sounding));
      writer.structural();
    }
  }

  /** Display version the symbols were spelled for. */
  public get version(): number {
    this.refresh();
    return this.spelledVersion;
  }

  /** @returns The shape symbol of segment `index` (capo applied), `''` outside the chart. */
  public shape(index: number): string {
    this.refresh();
    return this.shapes[index] ?? '';
  }

  /** @returns The sounding symbol (capo 0) of segment `index`, `''` outside the chart. */
  public soundingSymbol(index: number): string {
    this.refresh();
    return this.soundings[index] ?? '';
  }

  private refresh(): void {
    if (this.spelledVersion === this.display.version) {
      return;
    }
    const { segments, keys } = this.prepared;
    const shift = this.display.shift;
    this.shapes = segments.map((s, i) => chordSymbol(s, keys[i] ?? null, shift));
    this.soundings = segments.map((s, i) => chordSymbol(s, keys[i] ?? null, { transpose: shift.transpose, capo: 0 }));
    this.spelledVersion = this.display.version;
  }
}

/** Whether `element` already holds `shape` followed by a `.chord-sounding` child with `sounding`. */
function shows(element: HTMLElement, shape: string, sounding: string): boolean {
  const child = element.lastElementChild;
  const childShows = child?.className === SOUNDING_CLASS && child.textContent === sounding;
  return childShows && element.firstChild?.textContent === shape;
}

function soundingChild(doc: Document, sounding: string): HTMLElement {
  const span = doc.createElement('span');
  span.className = SOUNDING_CLASS;
  span.textContent = sounding;
  return span;
}
