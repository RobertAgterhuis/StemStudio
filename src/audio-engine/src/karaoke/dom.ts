/**
 * Banner DOM owned by the engine inside the (empty) Blazor host: three line slots, the ball
 * and the countdown. Text is set with `textContent` only; styling comes from the app's CSS.
 *
 * @packageDocumentation
 */
import type { KaraokeLine } from '../types';

/** Line slots, top to bottom. */
export const SLOT_NAMES = ['previous', 'current', 'next'] as const;

/** Name of a line slot (`data-slot`). */
export type SlotName = (typeof SLOT_NAMES)[number];

/** `data-state` of a word. */
export type WordState = 'past' | 'current' | 'future';

/** Host `dataset` keys written by the engine (removed again on detach). */
export const HOST_KEYS = ['state', 'lineIndex', 'wordIndex', 'positionMs'] as const;

/** The engine-created elements. */
export interface BannerDom {
  /** The host element (`data-testid="karaoke-banner"`). */
  readonly host: HTMLElement;
  /** `.karaoke-line` slots in {@link SLOT_NAMES} order. */
  readonly slots: readonly [HTMLElement, HTMLElement, HTMLElement];
  /** `.karaoke-ball`. */
  readonly ball: HTMLElement;
  /** `.karaoke-countdown` (three `.karaoke-dot` children, `data-count` lit). */
  readonly countdown: HTMLElement;
}

function element(doc: Document, className: string): HTMLElement {
  const created = doc.createElement('div');
  created.className = className;
  return created;
}

/**
 * Replaces the host's children with empty slots, the ball and the countdown.
 *
 * @param host - Host element.
 * @returns The created elements.
 */
export function buildBanner(host: HTMLElement): BannerDom {
  const doc = host.ownerDocument;
  const slots = SLOT_NAMES.map((name) => {
    const line = element(doc, 'karaoke-line');
    line.dataset.slot = name;
    return line;
  }) as [HTMLElement, HTMLElement, HTMLElement];
  const ball = element(doc, 'karaoke-ball');
  const countdown = element(doc, 'karaoke-countdown');
  countdown.append(...[0, 1, 2].map(() => element(doc, 'karaoke-dot')));
  countdown.dataset.count = '0';
  host.replaceChildren(...slots, ball, countdown);
  return { host, slots, ball, countdown };
}

/**
 * Fills a slot with the words of `line` (space-separated `span.karaoke-word`).
 *
 * @param slot - `.karaoke-line` element.
 * @param line - Line to show, undefined for an empty slot.
 * @param state - Initial `data-state` of every word.
 * @returns The word elements in order.
 */
export function fillSlot(slot: HTMLElement, line: KaraokeLine | undefined, state: WordState): HTMLElement[] {
  const words = (line?.words ?? []).map((word) => {
    const span = slot.ownerDocument.createElement('span');
    span.className = 'karaoke-word';
    span.textContent = word.text;
    span.dataset.state = state;
    return span;
  });
  slot.replaceChildren(...words.flatMap((span, i) => (i === 0 ? [span] : [' ', span])));
  return words;
}

/**
 * Writes `dataset[key]` only when it changes (no redundant DOM mutations per frame).
 *
 * @param target - Element.
 * @param key - `dataset` key.
 * @param value - New value.
 */
export function writeData(target: HTMLElement, key: string, value: string): void {
  if (target.dataset[key] !== value) {
    target.dataset[key] = value;
  }
}

/** @param host - Host whose engine children and {@link HOST_KEYS} attributes are removed. */
export function clearBanner(host: HTMLElement): void {
  host.replaceChildren();
  HOST_KEYS.forEach((key) => {
    host.removeAttribute(`data-${key.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`)}`);
  });
}
