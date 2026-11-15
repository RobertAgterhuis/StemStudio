/**
 * Focus helpers for the help panel, dialogs and tours (web-interop.md 2).
 *
 * @packageDocumentation
 */
import type { Disposable } from './types';
import { once } from './validation';

/**
 * Returns the `data-help-id` of the focused element or of its closest ancestor.
 *
 * @returns The help marker, or null when none applies.
 */
export function focusedHelpId(): string | null {
  return document.activeElement?.closest('[data-help-id]')?.getAttribute('data-help-id') ?? null;
}

/**
 * Remembers the focused element.
 *
 * @returns Handle that restores focus to that element on dispose (if it is still in the page).
 */
export function captureFocus(): Disposable {
  const element = document.activeElement;
  return once(() => {
    if (element instanceof HTMLElement && element.isConnected) {
      element.focus();
    }
  });
}
