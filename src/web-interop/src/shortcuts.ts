/**
 * Keyboard shortcuts (design-spec.md 6, web-interop.md 2): maps key presses to command ids and calls
 * `OnCommand(id)` on .NET. Keys are ignored while focus is in a text field, a select, an editable
 * element, a dialog or the tour popover. Space is never handled here (mixer-keys.js keeps it).
 *
 * @packageDocumentation
 */
import type { Disposable, DotNetRef, ShortcutScope } from './types';
import { once } from './validation';

/** Elements inside which shortcuts are ignored. */
const TYPING_CONTEXT = [
  'input',
  'textarea',
  'select',
  '[contenteditable="true"]',
  '[contenteditable=""]',
  '[role="dialog"]',
  '[data-testid="tour-popover"]',
].join(', ');

/** Help keys, active in every scope. */
const HELP_KEYS: Readonly<Record<string, string>> = { '?': 'help.open', F1: 'help.open' };

/** Key map per scope. */
const KEY_MAPS: Readonly<Record<ShortcutScope, Readonly<Record<string, string>>>> = { global: HELP_KEYS, mixer: HELP_KEYS };

/**
 * True when the event target is a place where keys belong to the user's typing or a dialog.
 *
 * @param target - The event target.
 * @returns Whether shortcuts must be ignored.
 */
export function isTypingContext(target: EventTarget | null): boolean {
  return target instanceof Element && target.closest(TYPING_CONTEXT) !== null;
}

/**
 * The command of a key press, or null when the key is not a shortcut here.
 *
 * @param event - The key event.
 * @param scope - The registered scope.
 * @returns The command id or null.
 */
export function commandFor(event: KeyboardEvent, scope: ShortcutScope): string | null {
  if (event.repeat || isTypingContext(event.target)) {
    return null;
  }

  return KEY_MAPS[scope][event.key] ?? null;
}

/**
 * Registers the key map and calls `dotnet.invokeMethodAsync("OnCommand", id)` with a command id.
 *
 * @param dotnet - Receiver of the command ids.
 * @param scope - `global` or `mixer` key map.
 * @returns Handle that removes the key listener.
 */
export function registerShortcuts(dotnet: DotNetRef, scope: ShortcutScope): Disposable {
  const onKeyDown = (event: KeyboardEvent): void => {
    const command = commandFor(event, scope);
    if (command !== null) {
      event.preventDefault();
      void dotnet.invokeMethodAsync('OnCommand', command);
    }
  };
  document.addEventListener('keydown', onKeyDown);
  return once(() => {
    document.removeEventListener('keydown', onKeyDown);
  });
}
