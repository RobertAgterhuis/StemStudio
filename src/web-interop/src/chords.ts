/**
 * Key chord helpers shared by the key map, the override validation and chord capture (web-interop 1.1.0 section 5).
 *
 * @packageDocumentation
 */
import type { KeyChord } from './types';

/** A key press reduced to what the key map looks at. */
export interface KeyInput {
  /** `KeyboardEvent.key` lower-cased. */
  readonly key: string;
  /** Ctrl (Cmd on macOS) held. */
  readonly mod: boolean;
  /** Shift held. */
  readonly shift: boolean;
  /** Alt/Option or the OS key held: never matches (rule 2). */
  readonly foreign: boolean;
}

/** Longest key name of a chord. */
export const MAX_KEY_LENGTH = 20;

const LETTER = /^[a-z]$/;

/**
 * The Shift rule of a chord: given, else forbidden for letters and not checked for other keys.
 *
 * @param chord - A chord.
 * @returns True (required), false (forbidden) or undefined (not checked).
 */
export function effectiveShift(chord: KeyChord): boolean | undefined {
  return chord.shift ?? (LETTER.test(chord.key) ? false : undefined);
}

/**
 * True when two chords can be triggered by the same key press.
 *
 * @param a - First chord.
 * @param b - Second chord.
 * @returns Whether they clash.
 */
export function chordsClash(a: KeyChord, b: KeyChord): boolean {
  const sa = effectiveShift(a);
  const sb = effectiveShift(b);
  return a.key === b.key && Boolean(a.mod) === Boolean(b.mod) && (sa === undefined || sb === undefined || sa === sb);
}

/**
 * True when a key press triggers the chord.
 *
 * @param chord - A chord.
 * @param input - The key press.
 * @returns Whether it matches.
 */
export function chordMatches(chord: KeyChord, input: KeyInput): boolean {
  const shift = effectiveShift(chord);
  return !input.foreign && chord.key === input.key && Boolean(chord.mod) === input.mod && (shift === undefined || shift === input.shift);
}

function isMac(): boolean {
  return /Mac|iPhone|iPad/.test(navigator.userAgent);
}

/**
 * Reads a key event: Mod is Cmd on macOS and Ctrl elsewhere; the other of the two and Alt are foreign.
 *
 * @param event - The key event.
 * @returns The key press.
 */
export function inputOf(event: KeyboardEvent): KeyInput {
  const mac = isMac();
  return {
    key: event.key.toLowerCase(),
    mod: mac ? event.metaKey : event.ctrlKey,
    shift: event.shiftKey,
    foreign: event.altKey || (mac ? event.ctrlKey : event.metaKey),
  };
}

/**
 * True for a structurally valid chord (key 1..20 characters, lower-case; optional booleans).
 *
 * @param value - Any value.
 * @returns Whether it is a chord.
 */
export function isChordShape(value: unknown): value is KeyChord {
  const chord = value as Partial<Record<'key' | 'mod' | 'shift', unknown>> | null;
  return typeof chord === 'object' && chord !== null && validKey(chord.key) && optionalBoolean(chord.mod) && optionalBoolean(chord.shift);
}

function validKey(key: unknown): boolean {
  return typeof key === 'string' && key.length >= 1 && key.length <= MAX_KEY_LENGTH && key === key.toLowerCase();
}

function optionalBoolean(value: unknown): boolean {
  return value === undefined || value === null || typeof value === 'boolean';
}

/**
 * Copies a chord without null or missing optional fields (JSON from .NET may carry nulls).
 *
 * @param chord - A structurally valid chord.
 * @returns A clean chord.
 */
export function cleanChord(chord: KeyChord): KeyChord {
  const mod = chord.mod === true ? { mod: true } : {};
  return typeof chord.shift === 'boolean' ? { key: chord.key, ...mod, shift: chord.shift } : { key: chord.key, ...mod };
}
