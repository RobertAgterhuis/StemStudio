/**
 * Keyboard help for native controls (feature 012, FR-017), document-wide and without a .NET round trip:
 * - `input[type=range][data-page-step]`: Page Up/Down move by that step (browsers use 10 % of the range) and fire
 *   `input` and `change` like a drag, so Blazor's handlers see the new value.
 * - `input[type=range][aria-readonly="true"]`: value keys are blocked (the speed slider while the trainer runs).
 * - `[data-ss-spin]` (custom spinbuttons): Arrow Up/Down do not scroll the page; Blazor handles the value.
 *
 * @packageDocumentation
 */
import type { Disposable } from './types';
import { once } from './validation';

/** Keys that change a range input's value. */
const VALUE_KEYS = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown']);

/** Keys a spinbutton handles itself. */
const SPIN_KEYS = new Set(['ArrowUp', 'ArrowDown']);

function isRange(target: Element): target is HTMLInputElement {
  return target instanceof HTMLInputElement && target.type === 'range';
}

function pageStep(input: HTMLInputElement, key: string): number {
  const step = Number(input.dataset.pageStep);
  return Number.isFinite(step) && (key === 'PageUp' || key === 'PageDown') ? (key === 'PageUp' ? step : -step) : 0;
}

function stepRange(input: HTMLInputElement, delta: number): void {
  const value = Math.min(Number(input.max), Math.max(Number(input.min), Number(input.value) + delta));
  input.value = String(value);
  input.dispatchEvent(new Event('input', { bubbles: true }));
  input.dispatchEvent(new Event('change', { bubbles: true }));
}

function handleRange(input: HTMLInputElement, event: KeyboardEvent): void {
  if (input.getAttribute('aria-readonly') === 'true') {
    if (VALUE_KEYS.has(event.key)) {
      event.preventDefault();
    }
    return;
  }

  const delta = pageStep(input, event.key);
  if (delta !== 0 && !input.disabled) {
    event.preventDefault();
    stepRange(input, delta);
  }
}

function onKeyDown(event: KeyboardEvent): void {
  const target = event.target;
  if (!(target instanceof Element)) {
    return;
  }

  if (isRange(target)) {
    handleRange(target, event);
  } else if (target.hasAttribute('data-ss-spin') && SPIN_KEYS.has(event.key)) {
    event.preventDefault();
  }
}

/**
 * Starts the control-key handling below a root.
 *
 * @param root - The document (or an element) to listen on.
 * @returns Handle that removes the listener.
 */
export function startControlKeys(root: Document | HTMLElement): Disposable {
  root.addEventListener('keydown', onKeyDown as EventListener);
  return once(() => {
    root.removeEventListener('keydown', onKeyDown as EventListener);
  });
}
