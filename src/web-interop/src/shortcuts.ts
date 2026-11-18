/**
 * Keyboard shortcuts (design-spec.md 6, web-interop.md 2): maps key presses to command ids and calls
 * `OnCommand(id)` on .NET. Keys are ignored while focus is in a text field, a select, an editable element, a
 * dialog or the tour popover (Mod+K excepted). Single keys never fire with a modifier other than Shift. Mod is Cmd on
 * macOS and Ctrl elsewhere.
 *
 * Without a key map a registration keeps the 1.0.0 tables below; with a key map (web-interop 1.1.0) only the given
 * bindings of that scope fire (`keymap.ts`), Space included, and {@link updateShortcuts} swaps the map in place.
 *
 * @packageDocumentation
 */
import { inputOf, type KeyInput } from './chords';
import { commandOf, compileKeyMap, focusContext, type CompiledChord } from './keymap';
import type { Disposable, DotNetRef, KeyMap, ShortcutScope } from './types';
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

/** How long the second key of a "G then X" sequence is awaited. */
export const SEQUENCE_TIMEOUT_MS = 1000;

interface Binding {
  readonly key: string;
  readonly command: string;
  readonly mod?: boolean;
  readonly shift?: boolean;
  readonly anywhere?: boolean;
}

const GLOBAL: readonly Binding[] = [
  { key: '?', command: 'help.open' },
  { key: 'f1', command: 'help.open' },
  { key: 'k', mod: true, shift: false, command: 'palette.open', anywhere: true },
  { key: '/', mod: true, command: 'shortcuts.open' },
];

const DIGITS: readonly Binding[] = Array.from({ length: 9 }, (_, i) => ({ key: String(i + 1), shift: false, command: `mixer.channel.select:${String(i + 1)}` }));

/** Practice tempo keys (feature 012, ui-test-ids 1.11.0 "Shortcut commands"); `+` is an alias of `=` (Shift+= or keypad). */
const PRACTICE: readonly Binding[] = [
  { key: '-', command: 'mixer.speed:-5' },
  { key: '=', command: 'mixer.speed:5' },
  { key: '+', command: 'mixer.speed:5' },
  { key: ',', command: 'mixer.transpose:-1' },
  { key: '.', command: 'mixer.transpose:1' },
  { key: 'r', command: 'mixer.practice.reset' },
  { key: 'c', command: 'mixer.countin.toggle' },
  { key: 't', command: 'mixer.trainer.toggle' },
];

const MIXER: readonly Binding[] = [
  { key: 'home', command: 'mixer.stop' },
  { key: 'arrowleft', shift: false, command: 'mixer.seek:-5' },
  { key: 'arrowleft', shift: true, command: 'mixer.seek:-1' },
  { key: 'arrowright', shift: false, command: 'mixer.seek:5' },
  { key: 'arrowright', shift: true, command: 'mixer.seek:1' },
  { key: '[', command: 'mixer.loop.a' },
  { key: ']', command: 'mixer.loop.b' },
  { key: 'l', shift: false, command: 'mixer.loop.toggle' },
  { key: 'k', shift: false, command: 'mixer.metronome.toggle' },
  { key: 'm', shift: false, command: 'mixer.channel.mute' },
  { key: 's', shift: false, command: 'mixer.channel.solo' },
  { key: 's', mod: true, shift: false, command: 'mixer.save' },
  { key: 's', mod: true, shift: true, command: 'mixer.save-as' },
  ...DIGITS,
  ...PRACTICE,
];

/** Library pages (feature 022, web-interop 1.2.0): `/` focuses the library search; Mod+/ stays the global overview. */
const LIBRARY: readonly Binding[] = [{ key: '/', mod: false, command: 'library.search.focus' }];

const KEY_MAPS: Readonly<Record<ShortcutScope, readonly Binding[]>> = { global: GLOBAL, mixer: MIXER, library: LIBRARY, setlist: [] };

/** Second keys of the global "G then X" sequences. */
const GO: Readonly<Record<string, string>> = { i: 'go.import', t: 'go.tracks', m: 'go.mixes' };

/**
 * True when the event target is a place where keys belong to the user's typing or a dialog.
 *
 * @param target - The event target.
 * @returns Whether shortcuts must be ignored.
 */
export function isTypingContext(target: EventTarget | null): boolean {
  return target instanceof Element && target.closest(TYPING_CONTEXT) !== null;
}

function modifiersMatch(binding: Binding, input: KeyInput): boolean {
  return Boolean(binding.mod) === input.mod && (binding.shift === undefined || binding.shift === input.shift);
}

function bindingFor(input: KeyInput, scope: ShortcutScope): Binding | undefined {
  return input.foreign ? undefined : KEY_MAPS[scope].find((b) => b.key === input.key && modifiersMatch(b, input));
}

function startsSequence(input: KeyInput, scope: ShortcutScope): boolean {
  return scope === 'global' && input.key === 'g' && !input.mod && !input.foreign;
}

/** Per-registration state of a pending "G then X" sequence. */
class Sequence {
  private timer: ReturnType<typeof setTimeout> | undefined;

  /** True while the second key is awaited. */
  public get pending(): boolean {
    return this.timer !== undefined;
  }

  /** Starts waiting for the second key. */
  public start(): void {
    this.timer = setTimeout(() => {
      this.timer = undefined;
    }, SEQUENCE_TIMEOUT_MS);
  }

  /** Stops waiting. */
  public stop(): void {
    clearTimeout(this.timer);
    this.timer = undefined;
  }
}

function sequenceCommand(input: KeyInput, sequence: Sequence): string | null {
  sequence.stop();
  return GO[input.key] ?? null;
}

/** A data-driven key map with its compiled chords (1.1.0). */
interface MapState {
  keyMap: KeyMap;
  compiled: CompiledChord[];
}

function commandFor(input: KeyInput, scope: ShortcutScope, target: EventTarget | null, map: MapState | null): string | null {
  if (map !== null) {
    return commandOf(map.compiled, map.keyMap, input, focusContext(target, input.key));
  }

  const binding = bindingFor(input, scope);
  return binding !== undefined && (!isTypingContext(target) || binding.anywhere === true) ? binding.command : null;
}

function sequenceAllowed(map: MapState | null): boolean {
  return map === null || map.keyMap.singleKey;
}

/** Repeats and keys another registration already handled (the global "G then M" before the mixer's M). */
function isSpent(event: KeyboardEvent): boolean {
  return event.repeat || event.defaultPrevented;
}

function handle(event: KeyboardEvent, scope: ShortcutScope, sequence: Sequence, map: MapState | null): string | null {
  if (isSpent(event)) {
    return null;
  }

  const input = inputOf(event);
  return sequence.pending ? sequenceCommand(input, sequence) : startOrCommand(event, input, { scope, sequence, map });
}

/** What a key press is handled against. */
interface HandleContext {
  readonly scope: ShortcutScope;
  readonly sequence: Sequence;
  readonly map: MapState | null;
}

function startOrCommand(event: KeyboardEvent, input: KeyInput, context: HandleContext): string | null {
  if (!isTypingContext(event.target) && startsSequence(input, context.scope) && sequenceAllowed(context.map)) {
    context.sequence.start();
    return null;
  }

  return commandFor(input, context.scope, event.target, context.map);
}

/** The current key map of a registration (null: the 1.0.0 tables). */
interface Holder {
  map: MapState | null;
}

/** Key map holder per registration handle, for {@link updateShortcuts}. */
const HOLDERS = new WeakMap<Disposable, Holder>();

function mapState(keyMap: KeyMap): MapState {
  return { keyMap, compiled: compileKeyMap(keyMap) };
}

/**
 * Registers the key map of a scope and calls `dotnet.invokeMethodAsync("OnCommand", id)` with a command id.
 *
 * @param dotnet - Receiver of the command ids.
 * @param scope - `global`, `mixer`, `library` or `setlist` key map.
 * @param keyMap - The bindings of the scope (1.1.0); omitted: the 1.0.0 built-in tables.
 * @returns Handle that removes the key listener.
 */
export function registerShortcuts(dotnet: DotNetRef, scope: ShortcutScope, keyMap?: KeyMap): Disposable {
  const sequence = new Sequence();
  const holder: Holder = { map: keyMap === undefined ? null : mapState(keyMap) };
  const onKeyDown = (event: KeyboardEvent): void => {
    const command = handle(event, scope, sequence, holder.map);
    if (command !== null) {
      event.preventDefault();
      void dotnet.invokeMethodAsync('OnCommand', command);
    }
  };
  document.addEventListener('keydown', onKeyDown);
  const registration = once(() => {
    sequence.stop();
    document.removeEventListener('keydown', onKeyDown);
  });
  HOLDERS.set(registration, holder);
  return registration;
}

/**
 * Swaps the key map of a registration without re-registering (1.1.0); ignored for an unknown handle.
 *
 * @param handle - A handle returned by {@link registerShortcuts}.
 * @param keyMap - The new key map.
 */
export function updateShortcuts(handle: Disposable, keyMap: KeyMap): void {
  const holder = HOLDERS.get(handle);
  if (holder !== undefined) {
    holder.map = mapState(keyMap);
  }
}
