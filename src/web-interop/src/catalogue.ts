/**
 * The default key map catalogue `keymap-defaults.json` (web-interop 1.1.0 section 5.4), imported at build time: the
 * same file `KeyMapDefaults` reads in C#. Gives the defaults per scope and the reserved-chord reasons.
 *
 * @packageDocumentation
 */
import raw from '../../StemStudio.Web/wwwroot/js/keymap-defaults.json';
import { chordsClash } from './chords';
import type { KeyBinding, KeyChord, KeyMap, ShortcutScope } from './types';

/** One command row of the catalogue. */
export interface CatalogueCommand {
  /** Command id. */
  readonly command: string;
  /** Key map scope. */
  readonly scope: ShortcutScope;
  /** Feature that provides the handler. */
  readonly owner: string;
  /** Overview group. */
  readonly group: string;
  /** Label. */
  readonly label: string;
  /** Default chords (0..2). */
  readonly defaults: readonly KeyChord[];
  /** Held for a feature not shipped yet. */
  readonly reserved: boolean;
  /** Fires in typing contexts too. */
  readonly anywhere?: boolean;
}

/** A pedal preset of the catalogue. */
export interface CataloguePedalPreset {
  /** Preset id. */
  readonly id: string;
  /** Label. */
  readonly label: string;
  /** Left pedal command, null for `custom`. */
  readonly left: string | null;
  /** Right pedal command, null for `custom`. */
  readonly right: string | null;
}

/** The parts of the catalogue the browser uses. */
export interface Catalogue {
  /** Catalogue version. */
  readonly version: string;
  /** Every command. */
  readonly commands: readonly CatalogueCommand[];
  /** Pedal defaults. */
  readonly pedal: { readonly defaultPreset: string; readonly leftKey: KeyChord; readonly rightKey: KeyChord; readonly presets: readonly CataloguePedalPreset[] };
  /** Never-bindable chords with the reason shown to the user. */
  readonly reservedChords: readonly (KeyChord & { readonly reason: string })[];
  /** Chords handled outside the key map (null `chord` for Alt chords). */
  readonly externalChords: readonly { readonly label: string; readonly chord?: KeyChord }[];
}

/** The catalogue. */
export const CATALOGUE = raw as unknown as Catalogue;

/** The plain key that starts the global "G then X" sequences: never bindable. */
export const SEQUENCE_KEY: KeyChord = { key: 'g' };

/**
 * The reason text when a chord can never be bound (rule 4, plus chords another feature handles), else null.
 *
 * @param chord - A chord.
 * @returns The reason, or null.
 */
export function isReservedChord(chord: KeyChord): string | null {
  const reserved = CATALOGUE.reservedChords.find((r) => chordsClash(r, chord));
  if (reserved !== undefined) {
    return reserved.reason;
  }

  const external = CATALOGUE.externalChords.find((e) => e.chord !== undefined && chordsClash(e.chord, chord));
  return external === undefined ? null : `Used elsewhere: ${external.label}`;
}

function pedalBindings(scope: ShortcutScope): KeyBinding[] {
  const preset = CATALOGUE.pedal.presets.find((p) => p.id === CATALOGUE.pedal.defaultPreset);
  const sides: [string | null | undefined, KeyChord][] = [
    [preset?.left, CATALOGUE.pedal.leftKey],
    [preset?.right, CATALOGUE.pedal.rightKey],
  ];
  return sides.flatMap(([command, chord]) => {
    const row = CATALOGUE.commands.find((c) => c.command === command && c.scope === scope);
    return row === undefined ? [] : [{ command: row.command, chords: [chord], layer: 'pedal' as const, owner: row.owner }];
  });
}

function keyBinding(row: CatalogueCommand): KeyBinding {
  const binding: KeyBinding = { command: row.command, chords: row.defaults, layer: 'keys', owner: row.owner, reserved: row.reserved };
  return row.anywhere === true ? { ...binding, anywhere: true } : binding;
}

/**
 * The default key map of a scope (spec FR-020): the catalogue rows of the scope plus the default pedal preset.
 *
 * @param scope - Key map scope.
 * @returns The default key map (single keys on, pedal mode off).
 */
export function defaultKeyMap(scope: ShortcutScope): KeyMap {
  const bindings = CATALOGUE.commands.filter((c) => c.scope === scope).map(keyBinding);
  return { bindings: [...bindings, ...pedalBindings(scope)], singleKey: true, pedalMode: false };
}
