/**
 * Keyboard shortcuts (design-spec.md 6, web-interop.md 2): maps key presses to command ids and calls
 * `OnCommand(id)` on .NET. Keys are ignored while focus is in a text field, a select, an editable element, a
 * dialog or the tour popover (Mod+K excepted). Single keys never fire with a modifier other than Shift. Space
 * is never handled here (mixer-keys.js keeps it). Mod is Cmd on macOS and Ctrl elsewhere.
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

/** How long the second key of a "G then X" sequence is awaited. */
export const SEQUENCE_TIMEOUT_MS = 1000;

interface Binding {
  readonly key: string;
  readonly command: string;
  readonly mod?: boolean;
  readonly shift?: boolean;
  readonly anywhere?: boolean;
}

interface KeyInput {
  readonly key: string;
  readonly mod: boolean;
  readonly shift: boolean;
  readonly foreign: boolean;
}

const GLOBAL: readonly Binding[] = [
  { key: '?', command: 'help.open' },
  { key: 'f1', command: 'help.open' },
  { key: 'k', mod: true, shift: false, command: 'palette.open', anywhere: true },
  { key: '/', mod: true, command: 'shortcuts.open' },
];

const DIGITS: readonly Binding[] = Array.from({ length: 9 }, (_, i) => ({ key: String(i + 1), shift: false, command: `mixer.channel.select:${String(i + 1)}` }));

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
];

const KEY_MAPS: Readonly<Record<ShortcutScope, readonly Binding[]>> = { global: GLOBAL, mixer: MIXER };

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

function isMac(): boolean {
  return /Mac|iPhone|iPad/.test(navigator.userAgent);
}

function inputOf(event: KeyboardEvent): KeyInput {
  const mac = isMac();
  return {
    key: event.key.toLowerCase(),
    mod: mac ? event.metaKey : event.ctrlKey,
    shift: event.shiftKey,
    foreign: event.altKey || (mac ? event.ctrlKey : event.metaKey),
  };
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

function commandFor(input: KeyInput, scope: ShortcutScope, typing: boolean): string | null {
  const binding = bindingFor(input, scope);
  return binding !== undefined && (!typing || binding.anywhere === true) ? binding.command : null;
}

/** Repeats and keys another registration already handled (the global "G then M" before the mixer's M). */
function isSpent(event: KeyboardEvent): boolean {
  return event.repeat || event.defaultPrevented;
}

function handle(event: KeyboardEvent, scope: ShortcutScope, sequence: Sequence): string | null {
  if (isSpent(event)) {
    return null;
  }

  const input = inputOf(event);
  const typing = isTypingContext(event.target);
  if (sequence.pending) {
    return sequenceCommand(input, sequence);
  }

  if (!typing && startsSequence(input, scope)) {
    sequence.start();
    return null;
  }

  return commandFor(input, scope, typing);
}

/**
 * Registers the key map of a scope and calls `dotnet.invokeMethodAsync("OnCommand", id)` with a command id.
 *
 * @param dotnet - Receiver of the command ids.
 * @param scope - `global` or `mixer` key map.
 * @returns Handle that removes the key listener.
 */
export function registerShortcuts(dotnet: DotNetRef, scope: ShortcutScope): Disposable {
  const sequence = new Sequence();
  const onKeyDown = (event: KeyboardEvent): void => {
    const command = handle(event, scope, sequence);
    if (command !== null) {
      event.preventDefault();
      void dotnet.invokeMethodAsync('OnCommand', command);
    }
  };
  document.addEventListener('keydown', onKeyDown);
  return once(() => {
    sequence.stop();
    document.removeEventListener('keydown', onKeyDown);
  });
}
