/**
 * Validation of the user's shortcut overrides (014 data-model 5.1, web-interop 1.1.0 section 5.3). The same rules as
 * `KeyMapValidator` in C# (tested on the same cases):
 * 1. A command entry is kept only for a known, non-reserved catalogue command whose value is a list of at most 2
 *    chords; at most 60 commands (in record order).
 * 2. A chord is dropped when its shape is invalid, it is reserved or handled elsewhere (`isReservedChord`), it is the
 *    plain `g` of the "G then X" sequences, or it clashes with a chord already taken. Taken first: the defaults of
 *    every command without an override (reserved commands included); then each accepted chord in order.
 * An overridden command never falls back to its defaults; an empty list unbinds it.
 *
 * @packageDocumentation
 */
import { CATALOGUE, SEQUENCE_KEY, isReservedChord } from './catalogue';
import { chordsClash, cleanChord, isChordShape } from './chords';
import type { KeyChord, KeyLayer, StoredChord } from './types';
import { isRecord } from './validation';

/** Most commands with an override. */
export const MAX_OVERRIDES = 60;

/** Most chords per command. */
export const MAX_CHORDS = 2;

/** Validated overrides plus a text per dropped entry. */
export interface OverridesResult {
  /** Command id -> accepted chords. */
  readonly overrides: Record<string, StoredChord[]>;
  /** One line per dropped command or chord (`<command>: <reason>`). */
  readonly dropped: string[];
}

interface Taken {
  readonly chord: KeyChord;
  readonly command: string;
}

const OVERRIDABLE = new Set(CATALOGUE.commands.filter((c) => !c.reserved).map((c) => c.command));

function structurallyValid(raw: unknown): [string, unknown[]][] {
  const entries = isRecord(raw) ? Object.entries(raw) : [];
  return entries
    .filter((entry): entry is [string, unknown[]] => OVERRIDABLE.has(entry[0]) && Array.isArray(entry[1]) && entry[1].length <= MAX_CHORDS)
    .slice(0, MAX_OVERRIDES);
}

function droppedCommands(raw: unknown, kept: readonly [string, unknown[]][]): string[] {
  const names = isRecord(raw) ? Object.keys(raw) : [];
  return names.filter((name) => !kept.some(([command]) => command === name)).map((name) => `${name}: not an overridable command or too many chords`);
}

const LAYERS: Readonly<Record<string, KeyLayer>> = { keys: 'keys', pedal: 'pedal' };

function layerOf(value: unknown): KeyLayer | null {
  const layer = isRecord(value) ? (value.layer ?? 'keys') : 'keys';
  return typeof layer === 'string' ? (LAYERS[layer] ?? null) : null;
}

function blocked(chord: KeyChord): string | null {
  return isReservedChord(chord) ?? (chordsClash(SEQUENCE_KEY, chord) ? 'Starts the "G then" sequences' : null);
}

function conflict(chord: KeyChord, taken: readonly Taken[]): string | null {
  const clash = taken.find((t) => chordsClash(t.chord, chord));
  return clash === undefined ? null : `conflicts with ${clash.command}`;
}

function rejection(value: unknown, taken: readonly Taken[]): string | null {
  if (!isChordShape(value) || layerOf(value) === null) {
    return 'invalid chord';
  }

  return blocked(value) ?? conflict(value, taken);
}

function storedChord(chord: KeyChord, value: unknown): StoredChord {
  return layerOf(value) === 'pedal' ? { ...cleanChord(chord), layer: 'pedal' } : cleanChord(chord);
}

function acceptChords(command: string, values: readonly unknown[], taken: Taken[], dropped: string[]): StoredChord[] {
  const accepted: StoredChord[] = [];
  for (const value of values) {
    const reason = rejection(value, taken);
    if (reason === null) {
      const chord = storedChord(value as KeyChord, value);
      accepted.push(chord);
      taken.push({ chord, command });
    } else {
      dropped.push(`${command}: ${reason}`);
    }
  }

  return accepted;
}

/**
 * Validates raw overrides (rules in the module comment).
 *
 * @param raw - The parsed `shortcuts.overrides` value.
 * @returns The accepted overrides and the dropped entries.
 */
export function validateOverrides(raw: unknown): OverridesResult {
  const kept = structurallyValid(raw);
  const dropped = droppedCommands(raw, kept);
  const overridden = new Set(kept.map(([command]) => command));
  const taken: Taken[] = CATALOGUE.commands
    .filter((c) => !overridden.has(c.command))
    .flatMap((c) => c.defaults.map((chord) => ({ chord, command: c.command })));
  const overrides: Record<string, StoredChord[]> = {};
  for (const [command, values] of kept) {
    overrides[command] = acceptChords(command, values, taken, dropped);
  }

  return { overrides, dropped };
}
