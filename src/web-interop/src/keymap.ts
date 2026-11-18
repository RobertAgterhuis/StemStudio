/**
 * Data-driven key matching (web-interop 1.1.0 section 5.2): which binding of a key map a key press triggers, given
 * where the focus is.
 *
 * @packageDocumentation
 */
import { chordMatches, chordsClash, type KeyInput } from './chords';
import type { KeyBinding, KeyChord, KeyMap } from './types';

/** Where the key press happened. */
export type FocusContext = 'none' | 'control' | 'text';

/** Rule 1, first list: typing contexts (only `anywhere` Mod chords fire). */
const TEXT_CONTEXT = [
  'input:not([type=range])',
  'textarea',
  'select',
  '[contenteditable="true"]',
  '[contenteditable=""]',
  '[role="dialog"]',
  '[data-testid="tour-popover"]',
].join(', ');

/** Rule 1, second list: native controls whose keys stay native (pedal-layer chords in pedal mode still fire). */
const CONTROL_CONTEXT = ['input[type=range]', '[role="slider"]', '[role="listbox"]', '[role="grid"]', '[role="menu"]', '[role="menubar"]', '[role="tablist"]'].join(', ');

/** Controls on which Space and Enter keep their native action. */
const ACTIVATABLE = ['button', 'a[href]', 'summary', '[role="button"]', '[role="checkbox"]', '[role="switch"]'].join(', ');

/** One chord of a binding, flattened. */
export interface CompiledChord {
  /** The chord. */
  readonly chord: KeyChord;
  /** Its binding. */
  readonly binding: KeyBinding;
}

/**
 * Classifies the focus target of a key press (rule 1).
 *
 * @param target - The event target.
 * @param key - The lower-cased key.
 * @returns `text`, `control` or `none`.
 */
export function focusContext(target: EventTarget | null, key: string): FocusContext {
  if (!(target instanceof Element)) {
    return 'none';
  }

  return target.closest(TEXT_CONTEXT) !== null ? 'text' : controlContext(target, key);
}

function controlContext(target: Element, key: string): FocusContext {
  const activation = (key === ' ' || key === 'enter') && target.closest(ACTIVATABLE) !== null;
  return activation || target.closest(CONTROL_CONTEXT) !== null ? 'control' : 'none';
}

/**
 * Flattens a key map; a chord that clashes with an earlier one is dropped and reported once on the console (rule 3).
 * Reserved bindings keep their chords taken but never fire.
 *
 * @param keyMap - The key map.
 * @returns The chords in order.
 */
export function compileKeyMap(keyMap: KeyMap): CompiledChord[] {
  const compiled: CompiledChord[] = [];
  const duplicates: string[] = [];
  for (const binding of keyMap.bindings) {
    for (const chord of binding.chords) {
      if (compiled.some((c) => chordsClash(c.chord, chord))) {
        duplicates.push(binding.command);
      } else {
        compiled.push({ chord, binding });
      }
    }
  }

  reportDuplicates(duplicates);
  return compiled;
}

function reportDuplicates(duplicates: readonly string[]): void {
  if (duplicates.length > 0) {
    console.warn(`StemStudio shortcuts: duplicate chords dropped for ${duplicates.join(', ')}`);
  }
}

function firesAnywhere(entry: CompiledChord): boolean {
  return entry.binding.anywhere === true && entry.chord.mod === true;
}

function firesOnControls(entry: CompiledChord, keyMap: KeyMap): boolean {
  return entry.binding.layer === 'pedal' && keyMap.pedalMode;
}

function allowedHere(entry: CompiledChord, context: FocusContext, keyMap: KeyMap): boolean {
  return context === 'none' || firesAnywhere(entry) || (context === 'control' && firesOnControls(entry, keyMap));
}

function active(entry: CompiledChord, keyMap: KeyMap): boolean {
  return entry.binding.reserved !== true && (keyMap.singleKey || entry.chord.mod === true);
}

/**
 * The command a key press triggers, or null.
 *
 * @param compiled - The compiled chords of the key map.
 * @param keyMap - The key map (for `singleKey` and `pedalMode`).
 * @param input - The key press.
 * @param context - Where the focus is.
 * @returns The command id, or null.
 */
export function commandOf(compiled: readonly CompiledChord[], keyMap: KeyMap, input: KeyInput, context: FocusContext): string | null {
  const entry = compiled.find((c) => chordMatches(c.chord, input));
  return entry !== undefined && active(entry, keyMap) && allowedHere(entry, context, keyMap) ? entry.binding.command : null;
}
