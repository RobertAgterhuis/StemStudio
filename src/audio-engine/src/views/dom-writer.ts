/**
 * Counted DOM writes of the engine views (contract 2.6.0 frame budget, research R-12): every write is skipped when the
 * value is unchanged, and every real write is counted so the tests can bound the work of one frame.
 *
 * @packageDocumentation
 */

/** Inline style properties the views write. */
export type WrittenStyle = 'transform' | 'width';

/** Writes data attributes, inline styles and text only when they change, and counts the writes. */
export class DomWriter {
  private count = 0;

  /** Real writes so far (unchanged values are not counted). */
  public get writes(): number {
    return this.count;
  }

  /**
   * @param target - Element.
   * @param key - `dataset` key.
   * @param value - New value.
   */
  public data(target: HTMLElement, key: string, value: string): void {
    if (target.dataset[key] !== value) {
      target.dataset[key] = value;
      this.count += 1;
    }
  }

  /**
   * @param target - Element.
   * @param property - Style property.
   * @param value - New value.
   */
  public style(target: HTMLElement, property: WrittenStyle, value: string): void {
    if (target.style[property] !== value) {
      target.style[property] = value;
      this.count += 1;
    }
  }

  /**
   * @param target - Element.
   * @param value - New text (set with `textContent`, never as markup).
   */
  public text(target: HTMLElement, value: string): void {
    if (target.textContent !== value) {
      target.textContent = value;
      this.count += 1;
    }
  }

  /** Counts a structural change (an element inserted or removed). */
  public structural(): void {
    this.count += 1;
  }
}

/**
 * @param value - A pixel value.
 * @returns The value rounded to 1/100 px, without a negative zero.
 */
export function px(value: number): string {
  const rounded = Math.round(value * 100) / 100;
  return `${String(rounded === 0 ? 0 : rounded)}px`;
}
